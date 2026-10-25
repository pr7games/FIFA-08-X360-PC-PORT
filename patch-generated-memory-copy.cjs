const fs=require('node:fs'),path=require('node:path');
const p=path.join(__dirname,'native/generated/default/fifa08_recomp.11.cpp');
let s=fs.readFileSync(p,'utf8').replace(/\r\n/g,'\n');
const shortAnchor='DEFINE_REX_FUNC(sub_8231F8D0) {\n\tREX_FUNC_PROLOGUE();\n';
const shortAfter=shortAnchor+'\tif (fifa08_copy_short(ctx, base)) return;\n';
if(!s.includes(shortAfter)) {
  if(s.split(shortAnchor).length!==2)throw new Error('Guest short memcpy anchor mismatch');
  s=s.replace(shortAnchor,shortAfter);
}
const anchor='loc_8231F934:\n';
const after=anchor+'\tif (fifa08_copy_aligned_bulk(ctx, base)) goto loc_8231F960;\n';
if(!s.includes(after)){
  if(s.split(anchor).length!==2)throw new Error('Guest memcpy loop anchor mismatch');
  s=s.replace(anchor,after);
}
if(!s.includes('#include "guest_copy_policy.h"'))s=s.replace('#include "fifa08_funcs.11.h"','#include "fifa08_funcs.11.h"\n#include "guest_copy_policy.h"');
if(s!==fs.readFileSync(p,'utf8'))fs.writeFileSync(p,s);
console.log('Guest memcpy: native nonoverlapping aligned bulk, original tail/register state retained.');
