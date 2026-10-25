#pragma once
#include <algorithm>
#include <chrono>
#include <cstdlib>
#include <stdexcept>
#include <atomic>
#include <condition_variable>
#include <deque>
#include <filesystem>
#include <fstream>
#include <functional>
#include <memory>
#include <mutex>
#include <sstream>
#include <thread>
#include <windows.h>
#include <mmsystem.h>
#include <imgui.h>
#include "achievement_presentation.h"
#include <rex/cvar.h>
#include <rex/runtime.h>
#include <rex/system/kernel_state.h>
#include <rex/system/achievement_manager.h>
#include <rex/ui/imgui_drawer.h>
#include <rex/ui/overlay/achievement_notification.h>
#include <rex/ui/overlay/achievement_icon_cache.h>

REXCVAR_DEFINE_BOOL(fifa08_achievement_popup, true, "FIFA08", "Show achievement notifications");
REXCVAR_DEFINE_BOOL(fifa08_achievement_sound, true, "FIFA08", "Play achievement notification sound");
namespace fifa08::achievements {
inline bool English(){const char* language=std::getenv("FIFA08_UI_LANGUAGE");return !language||std::string_view(language)!="es";}
inline std::string Escape(std::string_view s){std::string out;for(unsigned char c:s){if(c=='"'||c=='\\'){out+='\\';out+=char(c);}else if(c<32){char b[7];snprintf(b,sizeof(b),"\\u%04x",unsigned(c));out+=b;}else out+=char(c);}return out;}
inline std::string Hex(uint32_t id){char b[9];snprintf(b,sizeof(b),"%08X",id);return b;}
inline void AtomicWrite(const std::filesystem::path& path,std::string_view data){auto tmp=path;tmp+=".tmp";{std::ofstream f(tmp,std::ios::binary|std::ios::trunc);if(!f)throw std::runtime_error("Cannot write achievement catalog");f.write(data.data(),data.size());f.flush();if(!f)throw std::runtime_error("Cannot flush achievement catalog");}if(!MoveFileExW(tmp.c_str(),path.c_str(),MOVEFILE_REPLACE_EXISTING|MOVEFILE_WRITE_THROUGH))throw std::runtime_error("Cannot publish achievement catalog");}
// Exports only on title load/unlock. The runtime TOML remains authoritative.
// No invisible dialog and no disk IO in the frame or unlock callback.
class Exporter {
 struct Signal {std::mutex mutex;std::condition_variable cv;bool dirty=true,stop=false;};
 std::shared_ptr<Signal> signal_=std::make_shared<Signal>();std::thread worker_;rex::Runtime* runtime_=nullptr;rex::system::AchievementListenerHandle listener_=0;
 bool Export(){auto* ks=runtime_->kernel_state();auto& manager=ks->achievements();auto list=manager.ListAchievements();if(list.empty())return false;auto db=ks->title_xdbf();auto entries=db.is_valid()?db.GetAchievements():std::vector<rex::system::util::XdbfAchievementTableEntry>{};
  auto dir=runtime_->user_data_root()/"achievements"/Hex(ks->title_id());std::filesystem::create_directories(dir/"icons");std::ostringstream json;json<<"{\"schema\":1,\"title_id\":\""<<Hex(ks->title_id())<<"\",\"achievements\":[";
  bool first=true;for(const auto& a:list){if(!first)json<<',';first=false;json<<"{\"id\":"<<a.id<<",\"score\":"<<a.gamerscore<<",\"unlocked\":"<<(manager.IsUnlocked(a.id)?"true":"false")<<",\"unlocked_at\":"<<manager.GetUnlockTime(a.id)<<",\"icon\":\"icons/"<<a.image_id<<".png\"";
   for(auto language: {std::pair{rex::system::XLanguage(1),"en"},std::pair{rex::system::XLanguage(5),"es"}}){std::string name=a.label,description=a.description,locked=a.unachieved_description;for(const auto& e:entries)if(uint32_t(e.id)==a.id){auto lang=db.GetExistingLanguage(language.first);name=db.GetStringTableEntry(lang,e.label_id);description=db.GetStringTableEntry(lang,e.description_id);locked=db.GetStringTableEntry(lang,e.unachieved_id);break;}json<<",\"name_"<<language.second<<"\":\""<<Escape(name)<<"\",\"description_"<<language.second<<"\":\""<<Escape(description)<<"\",\"locked_"<<language.second<<"\":\""<<Escape(locked)<<'"';}
   json<<'}';auto icon=dir/"icons"/(std::to_string(a.image_id)+".png");if(a.image_id&&db.is_valid()&&!std::filesystem::exists(icon)){auto block=db.GetEntry(rex::system::util::XdbfSection::kImage,a.image_id);if(block)AtomicWrite(icon,std::string_view(reinterpret_cast<const char*>(block.buffer),block.size));}}
  json<<"]}";AtomicWrite(dir/"catalog.json",json.str());return true;
 }
 public:
 void Start(rex::Runtime* runtime){if(worker_.joinable()||!runtime||!runtime->kernel_state())return;runtime_=runtime;auto weak=std::weak_ptr<Signal>(signal_);listener_=runtime_->kernel_state()->achievements().RegisterUnlockCallback([weak](const auto&){if(auto s=weak.lock()){std::lock_guard lock(s->mutex);s->dirty=true;s->cv.notify_one();}});
  worker_=std::thread([this]{auto s=signal_;std::unique_lock lock(s->mutex);while(!s->stop){s->cv.wait(lock,[&]{return s->stop||s->dirty;});if(s->stop)break;s->dirty=false;lock.unlock();bool ready=false;try{ready=Export();}catch(const std::exception& e){REXLOG_WARN("FIFA08 achievement export: {}",e.what());}lock.lock();if(!ready&&!s->stop){s->cv.wait_for(lock,std::chrono::seconds(1),[&]{return s->stop;});s->dirty=true;}}});
 }
 void Stop(){if(listener_&&runtime_&&runtime_->kernel_state())runtime_->kernel_state()->achievements().UnregisterCallback(listener_);listener_=0;{std::lock_guard lock(signal_->mutex);signal_->stop=true;signal_->cv.notify_one();}if(worker_.joinable())worker_.join();runtime_=nullptr;}
 ~Exporter(){Stop();}
};

inline rex::system::AchievementInfo Localized(rex::Runtime* runtime,rex::system::AchievementInfo info){
 auto db=runtime->kernel_state()->title_xdbf();if(db.is_valid())for(const auto& e:db.GetAchievements())if(uint32_t(e.id)==info.id){auto lang=db.GetExistingLanguage(rex::system::XLanguage(English()?1:5));
 auto assign=[](std::string& target,std::string value){if(!value.empty())target=std::move(value);};
 assign(info.label,db.GetStringTableEntry(lang,e.label_id));assign(info.description,db.GetStringTableEntry(lang,e.description_id));assign(info.unachieved_description,db.GetStringTableEntry(lang,e.unachieved_id));break;}return info;
}
class Panel final:public rex::ui::ImGuiDialog {
 rex::system::AchievementManager* manager_;rex::ui::AchievementIconCache icons_;std::vector<rex::system::AchievementInfo> catalog_;int filter_=0;std::function<void()> close_;
 public:Panel(rex::ui::ImGuiDrawer* drawer,rex::ui::ImmediateDrawer* immediate,rex::Runtime* runtime,std::function<void()> close):ImGuiDialog(drawer),close_(std::move(close)),manager_(&runtime->kernel_state()->achievements()),icons_(immediate,runtime){for(auto a:manager_->ListAchievements())catalog_.push_back(Localized(runtime,std::move(a)));}
 protected:void OnDraw(ImGuiIO& io)override{std::vector<presentation::Row> rows;rows.reserve(catalog_.size());for(const auto& a:catalog_){bool done=manager_->IsUnlocked(a.id);rows.push_back({a.id,a.gamerscore,done,a.label,done?a.description:a.unachieved_description,reinterpret_cast<ImTextureID>(icons_.GetIcon(a))});}if(!presentation::Panel(io,rows,filter_,English())&&close_)close_();}
};

// State survives queued callbacks; owner is accessed only on the UI thread.
class Toast final:public rex::ui::AchievementNotificationDialog {
 struct State {std::mutex mutex;std::deque<rex::system::AchievementEvent> queue;Toast* owner=nullptr;};
 std::shared_ptr<State> state_=std::make_shared<State>();std::function<void(std::function<void()>)> dispatch_;rex::ui::AchievementIconCache icons_;rex::Runtime* runtime_;std::chrono::steady_clock::time_point shown_{};bool attached_=false;
 void Attach(){if(!attached_){imgui_drawer()->AddDialog(this);attached_=true;}}
 public:
 Toast(rex::ui::ImGuiDrawer* drawer,rex::ui::ImmediateDrawer* immediate,rex::Runtime* runtime,std::function<void(std::function<void()>)> dispatch):AchievementNotificationDialog(drawer),dispatch_(std::move(dispatch)),icons_(immediate,runtime),runtime_(runtime){state_->owner=this;drawer->RemoveDialog(this);}
 ~Toast()override{state_->owner=nullptr;}
 void Push(const rex::system::AchievementEvent& original)override{if(!REXCVAR_GET(fifa08_achievement_popup))return;auto event=original;event.achievement=Localized(runtime_,event.achievement);
  {std::lock_guard lock(state_->mutex);state_->queue.push_back(std::move(event));}auto s=state_;dispatch_([s]{if(s->owner)s->owner->Attach();});
 }
 protected:
 void OnDraw(ImGuiIO& io)override{const auto now=std::chrono::steady_clock::now();rex::system::AchievementEvent event;bool sound=false;float age=0;
  {std::lock_guard lock(state_->mutex);if(!state_->queue.empty()&&shown_!=std::chrono::steady_clock::time_point{}&&now-shown_>=std::chrono::duration<float>(presentation::ToastSeconds)){state_->queue.pop_front();shown_={};}if(state_->queue.empty()){shown_={};imgui_drawer()->RemoveDialog(this);attached_=false;return;}if(shown_==std::chrono::steady_clock::time_point{}){shown_=now;sound=true;}event=state_->queue.front();age=std::chrono::duration<float>(now-shown_).count();}
  if(sound&&REXCVAR_GET(fifa08_achievement_sound)){
   // Playback is asynchronous; never sleep or wait on the render thread.
   static bool opened=false;
   if(!opened){const wchar_t* path=_wgetenv(L"FIFA08_ACHIEVEMENT_SOUND");
    if(path&&*path){std::wstring command=L"open \""+std::wstring(path)+L"\" type mpegvideo alias fifa08_achievement";
     opened=mciSendStringW(command.c_str(),nullptr,0,nullptr)==0;}}
   if(opened)mciSendStringW(L"play fifa08_achievement from 0",nullptr,0,nullptr);
   else MessageBeep(MB_OK);
  }
  const auto& a=event.achievement;presentation::Row row{a.id,a.gamerscore,true,a.label,a.description.empty()?a.unachieved_description:a.description,reinterpret_cast<ImTextureID>(icons_.GetIcon(a))};presentation::Toast(io,row,age,English());
 }
};
}
