#pragma once
#include <imgui.h>
#include <algorithm>
#include <cmath>
#include <cstdio>
#include <string>
#include <vector>

namespace fifa08::achievements::presentation {
inline ImFont* font=nullptr;
struct Row {unsigned id=0,score=0;bool unlocked=false;std::string name,description;ImTextureID icon=0;};
inline float Scale(const ImGuiIO& io){return std::clamp(std::min(io.DisplaySize.x/1280.f,io.DisplaySize.y/720.f),.75f,1.8f);}
inline float Ease(float t){t=std::clamp(t,0.f,1.f);return 1.f-(1.f-t)*(1.f-t)*(1.f-t);}
struct Motion {float reveal,alpha,slide;};
inline constexpr float ToastSeconds=7.0f;
inline Motion Animate(float age){float enter=Ease(age/.40f),exit=Ease((ToastSeconds-age)/.42f);return {Ease((age-.18f)/.55f),std::min(enter,exit),1.f-std::min(enter,exit)};}
inline void Trophy(ImDrawList* d,ImVec2 c,float r,ImU32 color){
 d->AddCircle(c,r,color,32,1.6f);float u=r*.085f;
 d->AddRect(ImVec2(c.x-5*u,c.y-5*u),ImVec2(c.x+5*u,c.y+1*u),color,2*u,0,2*u);
 d->AddBezierCubic(ImVec2(c.x-5*u,c.y-4*u),ImVec2(c.x-11*u,c.y-5*u),ImVec2(c.x-9*u,c.y+3*u),ImVec2(c.x-3*u,c.y+2*u),color,1.5f*u);
 d->AddBezierCubic(ImVec2(c.x+5*u,c.y-4*u),ImVec2(c.x+11*u,c.y-5*u),ImVec2(c.x+9*u,c.y+3*u),ImVec2(c.x+3*u,c.y+2*u),color,1.5f*u);
 d->AddLine(ImVec2(c.x,c.y+1*u),ImVec2(c.x,c.y+6*u),color,2*u);d->AddLine(ImVec2(c.x-4*u,c.y+6*u),ImVec2(c.x+4*u,c.y+6*u),color,2*u);
}
// Truncate only at UTF-8 boundaries; fitting matches the font and wrap width.
inline std::string FitText(ImFont* font,float size,const std::string& text,float width,int lines){
 if(text.empty())return {};std::string result=text;
 if(font->CalcTextSizeA(size,100000.f,width,result.c_str()).y<=size*lines+.5f)return result;
 do{size_t end=result.size()-1;while(end>0&&(static_cast<unsigned char>(result[end])&0xC0)==0x80)--end;result.resize(end);}
 while(!result.empty()&&font->CalcTextSizeA(size,100000.f,width,(result+"...").c_str()).y>size*lines+.5f);
 return result+"...";
}
inline void Toast(const ImGuiIO& io,const Row& row,float age,bool english=true){
 ImGui::PushFont(font,16.f);
 const float s=Scale(io)*.55f,h=162*s,full=std::min(760*s,io.DisplaySize.x-24*s);auto m=Animate(age);float width=h+(full-h)*m.reveal;
 ImGui::SetNextWindowPos(ImVec2(io.DisplaySize.x*.5f,io.DisplaySize.y-32*s+38*s*m.slide),ImGuiCond_Always,ImVec2(.5f,1));ImGui::SetNextWindowSize(ImVec2(width,h));
 ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding,ImVec2(0,0));ImGui::PushStyleVar(ImGuiStyleVar_WindowBorderSize,0);ImGui::PushStyleVar(ImGuiStyleVar_Alpha,m.alpha);
 if(ImGui::Begin("##fifa08_achievement_toast",nullptr,ImGuiWindowFlags_NoDecoration|ImGuiWindowFlags_NoInputs|ImGuiWindowFlags_NoSavedSettings|ImGuiWindowFlags_NoFocusOnAppearing|ImGuiWindowFlags_NoBackground)){
  auto* d=ImGui::GetWindowDrawList();auto p=ImGui::GetWindowPos();ImVec2 end(p.x+width,p.y+h);auto color=[](ImVec4 c){return ImGui::GetColorU32(c);};
  d->AddRectFilled(p,end,color(ImVec4(.025f,.035f,.04f,.98f)),h*.5f);
  d->AddRect(p,end,color(ImVec4(.43f,.75f,.43f,.6f)),h*.5f,0,1.2f*s);
  ImVec2 badge(p.x+56*s,p.y+h*.5f);d->AddCircleFilled(badge,38*s,color(ImVec4(.08f,.36f,.12f,1)),48);Trophy(d,badge,27*s,color(ImVec4(.9f,1,.9f,1)));
  const float content=std::clamp((m.reveal-.45f)/.55f,0.f,1.f);
  if(content>0){ImGui::PushStyleVar(ImGuiStyleVar_Alpha,m.alpha*content);float x=p.x+112*s;
   if(row.icon){d->AddImage(row.icon,ImVec2(x,p.y+h*.5f-32*s),ImVec2(x+64*s,p.y+h*.5f+32*s),ImVec2(0,0),ImVec2(1,1),ImGui::GetColorU32(ImVec4(1,1,1,1)));x+=80*s;}
   const float right=p.x+full-98*s,wrap=std::max(1.f,right-x-12*s);
   d->PushClipRect(ImVec2(x,p.y+12*s),ImVec2(right,p.y+h-12*s),true);
   auto textFont=ImGui::GetFont();float nameSize=36*s,descriptionSize=26*s;
   d->AddText(textFont,21*s,ImVec2(x,p.y+17*s),color(ImVec4(.66f,.9f,.68f,1)),english?"ACHIEVEMENT UNLOCKED":"LOGRO DESBLOQUEADO");
   auto title=FitText(textFont,nameSize,row.name,wrap,1);
   d->AddText(textFont,nameSize,ImVec2(x,p.y+45*s),color(ImVec4(1,1,1,1)),title.c_str(),nullptr,wrap);
   float titleHeight=textFont->CalcTextSizeA(nameSize,100000.f,wrap,title.c_str()).y;
   auto description=FitText(textFont,descriptionSize,row.description,wrap,2);
   d->AddText(textFont,descriptionSize,ImVec2(x,p.y+45*s+titleHeight+7*s),color(ImVec4(.8f,.84f,.85f,1)),description.c_str(),nullptr,wrap);d->PopClipRect();
   char score[24];snprintf(score,sizeof(score),"%uG",row.score);d->AddText(textFont,36*s,ImVec2(right+8*s,p.y+h*.5f-18*s),color(ImVec4(.84f,.96f,.84f,1)),score);ImGui::PopStyleVar();
  }
 }ImGui::End();ImGui::PopStyleVar(3);ImGui::PopFont();
}
inline bool Panel(const ImGuiIO& io,const std::vector<Row>& rows,int& filter,bool english=true){
 bool open=true;
 ImGui::PushFont(font,16.f);
 float s=std::min(1.0f,Scale(io))*.97f,width=std::min(820*s,io.DisplaySize.x-32*s),height=std::min(590*s,io.DisplaySize.y-36*s);
 ImGui::SetNextWindowPos(ImVec2(io.DisplaySize.x*.5f,io.DisplaySize.y*.5f),ImGuiCond_Always,ImVec2(.5f,.5f));ImGui::SetNextWindowSize(ImVec2(width,height));
 ImGui::PushStyleVar(ImGuiStyleVar_WindowRounding,16*s);ImGui::PushStyleVar(ImGuiStyleVar_WindowBorderSize,1*s);ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding,ImVec2(24*s,20*s));ImGui::PushStyleVar(ImGuiStyleVar_FrameRounding,7*s);ImGui::PushStyleVar(ImGuiStyleVar_ItemSpacing,ImVec2(10*s,10*s));
 ImGui::PushStyleColor(ImGuiCol_WindowBg,ImVec4(.06f,.07f,.09f,.98f));ImGui::PushStyleColor(ImGuiCol_Border,ImVec4(.55f,.62f,.73f,.28f));ImGui::PushStyleColor(ImGuiCol_Text,ImVec4(.94f,.96f,1,1));ImGui::PushStyleColor(ImGuiCol_FrameBg,ImVec4(.13f,.15f,.19f,1));ImGui::PushStyleColor(ImGuiCol_PlotHistogram,ImVec4(.86f,.12f,.18f,1));ImGui::PushStyleColor(ImGuiCol_Button,ImVec4(.14f,.17f,.22f,1));ImGui::PushStyleColor(ImGuiCol_ButtonHovered,ImVec4(.22f,.29f,.36f,1));ImGui::PushStyleColor(ImGuiCol_ButtonActive,ImVec4(.45f,.12f,.16f,1));
 ImGui::PushStyleColor(ImGuiCol_TitleBg,ImVec4(.20f,.06f,.08f,1));
 ImGui::PushStyleColor(ImGuiCol_TitleBgActive,ImVec4(.48f,.08f,.12f,1));
 ImGui::PushStyleColor(ImGuiCol_TitleBgCollapsed,ImVec4(.20f,.06f,.08f,1));
 ImGui::PushStyleColor(ImGuiCol_ScrollbarBg,ImVec4(.10f,.10f,.12f,1));
 ImGui::PushStyleColor(ImGuiCol_ScrollbarGrab,ImVec4(.65f,.12f,.18f,1));
 ImGui::PushStyleColor(ImGuiCol_ScrollbarGrabHovered,ImVec4(.82f,.15f,.21f,1));
 ImGui::PushStyleColor(ImGuiCol_ScrollbarGrabActive,ImVec4(.95f,.18f,.24f,1));
 if(ImGui::Begin(english?"Achievements###fifa08_panel":"Logros###fifa08_panel",&open,ImGuiWindowFlags_NoResize|ImGuiWindowFlags_NoCollapse|ImGuiWindowFlags_NoSavedSettings)){
  ImGui::PushFont(font,20*s);unsigned unlocked=0,earned=0,total=0;for(auto& r:rows){total+=r.score;if(r.unlocked){unlocked++;earned+=r.score;}}
  ImGui::PushFont(font,30*s);ImGui::TextUnformatted(english?"Achievements":"Logros");ImGui::PopFont();ImGui::PushFont(font,15*s);ImGui::SameLine();ImGui::TextDisabled("%s",english?"  FIFA 08   /   F7 to close":"  FIFA 08   /   F7 para cerrar");ImGui::PopFont();
  ImGui::Text(english?"%u / %u unlocked":"%u / %u desbloqueados",unlocked,unsigned(rows.size()));ImGui::SameLine();ImGui::TextColored(ImVec4(.96f,.35f,.39f,1),"   %u / %uG",earned,total);
  char progress[24];snprintf(progress,sizeof(progress),"%.0f%%",rows.empty()?0.f:100.f*unlocked/rows.size());ImGui::ProgressBar(rows.empty()?0.f:float(unlocked)/rows.size(),ImVec2(-1,20*s),progress);
  const char* labels[]={english?"All":"Todos",english?"Unlocked":"Desbloqueados",english?"Locked":"Bloqueados"};for(int i=0;i<3;i++){if(i)ImGui::SameLine();bool selected=filter==i;if(selected)ImGui::PushStyleColor(ImGuiCol_Button,ImVec4(.4f,.10f,.14f,1));if(ImGui::Button(labels[i],ImVec2(126*s,32*s)))filter=i;if(selected)ImGui::PopStyleColor();}
  ImGui::TextDisabled("%s",english?"Saved automatically on this PC":"Guardado autom�ticamente en este PC");
  ImGui::BeginChild("##achievement_cards",ImVec2(0,0),false);
  int shown=0;
  for(const auto& r:rows){if((filter==1&&!r.unlocked)||(filter==2&&r.unlocked))continue;shown++;ImGui::PushID(int(r.id));auto p=ImGui::GetCursorScreenPos();float w=ImGui::GetContentRegionAvail().x;auto* d=ImGui::GetWindowDrawList();d->ChannelsSplit(2);d->ChannelsSetCurrent(1);
   ImGui::SetCursorScreenPos(ImVec2(p.x+16*s,p.y+16*s));if(r.icon)ImGui::ImageWithBg(r.icon,ImVec2(64*s,64*s),ImVec2(0,0),ImVec2(1,1),ImVec4(0,0,0,0),r.unlocked?ImVec4(1,1,1,1):ImVec4(.65f,.68f,.72f,1));else {Trophy(d,ImVec2(p.x+48*s,p.y+48*s),23*s,ImGui::GetColorU32(ImVec4(.55f,.65f,.73f,1)));ImGui::Dummy(ImVec2(64*s,64*s));}ImGui::SameLine(0,18*s);ImGui::BeginGroup();
   float tx=ImGui::GetCursorScreenPos().x;ImGui::PushTextWrapPos(ImGui::GetCursorPosX()+std::max(1.f,p.x+w-16*s-tx));ImGui::TextColored(r.unlocked?ImVec4(.96f,.35f,.39f,1):ImVec4(.65f,.69f,.75f,1),"%s   /   %uG",r.unlocked?(english?"UNLOCKED":"DESBLOQUEADO"):(english?"LOCKED":"BLOQUEADO"),r.score);ImGui::TextUnformatted(r.name.c_str());ImGui::PushStyleColor(ImGuiCol_Text,ImVec4(.76f,.8f,.86f,1));ImGui::TextUnformatted(r.description.c_str());ImGui::PopStyleColor();ImGui::PopTextWrapPos();ImGui::EndGroup();
   float end=std::max(p.y+96*s,ImGui::GetItemRectMax().y+16*s);d->ChannelsSetCurrent(0);d->AddRectFilled(p,ImVec2(p.x+w,end),ImGui::GetColorU32(r.unlocked?ImVec4(.18f,.09f,.11f,1):ImVec4(.095f,.11f,.14f,1)),10*s);d->AddRect(p,ImVec2(p.x+w,end),ImGui::GetColorU32(r.unlocked?ImVec4(.75f,.16f,.21f,.35f):ImVec4(.5f,.57f,.67f,.15f)),10*s);d->ChannelsMerge();ImGui::SetCursorScreenPos(ImVec2(p.x,end+10*s));ImGui::Dummy(ImVec2(w,0));ImGui::PopID();
  }if(!shown)ImGui::TextDisabled(rows.empty()?(english?"Launch the game to load its achievements.":"Inicia el juego para cargar sus logros."):(english?"No achievements in this filter.":"No hay logros con este filtro."));ImGui::EndChild();ImGui::PopFont();
 }ImGui::End();ImGui::PopStyleColor(15);ImGui::PopStyleVar(5);ImGui::PopFont();return open;
}
} // namespace fifa08::achievements::presentation
