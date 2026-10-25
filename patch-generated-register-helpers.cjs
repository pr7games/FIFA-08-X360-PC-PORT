// Expose exact register-save/restore bodies to the host compiler at direct
// call sites. Keep original exported guest functions and indirect dispatch.
const fs=require('node:fs'),path=require('node:path');
const root=path.join(__dirname,'native/generated/default');
const files=fs.readdirSync(root).filter(n=>/^fifa08_recomp\.\d+\.cpp$/.test(n));
const bodies=new Map();
for(const f of files){const s=fs.readFileSync(path.join(root,f),'utf8');
 for(const m of s.matchAll(/DEFINE_REX_FUNC\((__(?:save|rest)gprlr_\d+)\) \{([\s\S]*?)\n\}/g)){
  if(!/^__(?:save|rest)gprlr_(?:1[4-9]|2[0-9]|3[01])$/.test(m[1]))throw new Error('Unexpected register helper');
  if(/goto |REX_CALL|sub_|ctx\.fpscr/.test(m[2]))throw new Error('Unexpected helper side effects');
  bodies.set(m[1],m[2]);
 }
}
if(bodies.size!==36)throw new Error(`Expected 36 integer register helpers, found ${bodies.size}`);
const header='#pragma once\n// Exact translated bodies; no guest memory access or register update removed.\n'+
 [...bodies].sort().map(([name,body])=>`inline void fifa08_inline${name}(PPCContext& ctx, uint8_t* base) {${body}\n}\n`).join('\n');
const hp=path.join(__dirname,'native/src/guest_register_helpers.h');
if(!fs.existsSync(hp)||fs.readFileSync(hp,'utf8')!==header)fs.writeFileSync(hp,header);
let count=0,updated=0;
for(const f of files){const p=path.join(root,f),original=fs.readFileSync(p,'utf8');
 let s=original.replace(/\t((?:fifa08_inline)?__(?:save|rest)gprlr_\d+)\(ctx, base\);/g,(all,name)=>{
  const plain=name.replace(/^fifa08_inline/,'');if(!bodies.has(plain))throw new Error('Missing helper body');
  ++count;return '\tfifa08_inline'+plain+'(ctx, base);';
 });
 if(s.includes('\tfifa08_inline__')&&!s.includes('#include "guest_register_helpers.h"'))s=s.replace(/(#include "fifa08_funcs\.\d+\.h")/,'$1\n#include "guest_register_helpers.h"');
 if(s!==original){fs.writeFileSync(p,s);updated++;}
}
console.log(`Register helpers: 36 exact bodies, ${count} direct sites, ${updated} partitions updated; guest entry points retained.`);
