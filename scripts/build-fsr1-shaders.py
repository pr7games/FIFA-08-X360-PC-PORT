"""Rebuild presentation shaders using AMD FidelityFX FSR tag v1.0.2."""
from pathlib import Path
import argparse,hashlib,json,struct,subprocess
ap=argparse.ArgumentParser();ap.add_argument('--fxc',required=True);ap.add_argument('--glslang',required=True);ap.add_argument('--work',required=True);args=ap.parse_args()
root=Path(__file__).resolve().parents[1];sdk=root/'sdk-overlay';src=sdk/'src/ui/shaders/fsr1';work=Path(args.work);work.mkdir(parents=True,exist_ok=True)
up=sdk/'thirdparty/fidelityfx-fsr-1.0.2';meta=json.loads((up/'UPSTREAM.json').read_text())
for name,entry in meta['files'].items():
    assert hashlib.sha256((up/name).read_bytes()).hexdigest()==entry['sha256'],name
results=[]
for effect,defines in [('easu',['EASU=1']),('rcas',[]),('rcas_dither',['DITHER=1'])]:
    name='guest_output_ffx_fsr_'+effect+'_ps'
    for api in ['d3d12','vulkan']:
        binary=work/(name+('.dxbc' if api=='d3d12' else '.spv'))
        if api=='d3d12':
            cmd=[args.fxc,'/nologo','/O3','/Ges','/T','ps_5_1','/E','main','/Fo',str(binary)]
            for d in defines:cmd+=['/D',d]
            cmd+=[str(src/'fsr1.ps.hlsl')]
        else:
            cmd=[args.glslang,'-V','--target-env','vulkan1.0','-S','frag','-o',str(binary)]
            cmd+=['-D'+d for d in defines]+[str(src/'fsr1.frag')]
        subprocess.run(cmd,check=True)
        b=binary.read_bytes()
        if api=='d3d12':
            assert b[:4]==b'DXBC';data=list(b);ctype='BYTE';folder='bytecode/d3d12_5_1'
        else:
            assert struct.unpack_from('<I',b)[0]==0x07230203;data=list(struct.unpack('<'+'I'*(len(b)//4),b));ctype='uint32_t';folder='vulkan_spirv'
        out=sdk/'src/ui/shaders'/folder/(name+'.h');out.parent.mkdir(parents=True,exist_ok=True)
        text='// Generated from unmodified AMD FidelityFX FSR v1.0.2 headers.\n// Rebuild with scripts/build-fsr1-shaders.py; see thirdparty/fidelityfx-fsr-1.0.2/LICENSE.txt.\nconst '+ctype+' '+name+'[] = {\n'
        text+='\n'.join('  '+', '.join(str(x) for x in data[i:i+12])+',' for i in range(0,len(data),12))+'\n};\n'
        out.write_text(text)
        results.append({'api':api,'effect':effect,'bytes':len(b),'sha256':hashlib.sha256(b).hexdigest(),'header':str(out.relative_to(sdk))})
(work/'shader-build.json').write_text(json.dumps(results,indent=2)+'\n')
print('Generated all six official FSR 1.0.2 shaders.')
