"""Download upstream source dependencies and restore reviewed v1.0.0 overlay."""
from pathlib import Path
import argparse,hashlib,io,json,subprocess,sys,urllib.request,zipfile,shutil
root=Path(__file__).resolve().parents[1]
sdk=root/'tools/rexglue-source/rexglue-sdk-0.10.0'
p=argparse.ArgumentParser();p.add_argument('--archive',type=Path);p.add_argument('--sdk-only',action='store_true');args=p.parse_args()
manifest=json.loads((root/'sdk-overlay/manifest.json').read_text())
if not (sdk/'CMakeLists.txt').exists():
 data=args.archive.read_bytes() if args.archive else urllib.request.urlopen(urllib.request.Request('https://codeload.github.com/rexglue/rexglue-sdk/zip/refs/tags/v0.10.0',headers={'User-Agent':'FIFA08-RECOMP-PR7/1.0.0'}),timeout=120).read()
 archive=zipfile.ZipFile(io.BytesIO(data));prefix=archive.namelist()[0]
 # Validate pristine files which will be replaced, before extracting anything.
 for entry in manifest['files']:
  if entry['upstream_sha256'] is not None:
   source=archive.read(prefix+entry['path'])
   if hashlib.sha256(source).hexdigest()!=entry['upstream_sha256']:raise RuntimeError('Unexpected upstream revision: '+entry['path'])
 boundary=sdk.resolve();sdk.mkdir(parents=True,exist_ok=True)
 for member in archive.infolist():
  rel=Path(*Path(member.filename).parts[1:])
  dest=(sdk/rel).resolve()
  if not dest.is_relative_to(boundary):raise RuntimeError('Unsafe archive member')
  if member.is_dir():dest.mkdir(parents=True,exist_ok=True)
  else:dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(archive.read(member))
# Download submodule revisions and preserve their upstream licenses.
if not args.sdk_only:
 subprocess.run([sys.executable,str(root/'tools/prepare-sdk-dependencies.py')],cwd=root,check=True)
for entry in manifest['files']:
 src=root/'sdk-overlay'/entry['path'];data=src.read_bytes()
 if hashlib.sha256(data).hexdigest()!=entry['sha256']:raise RuntimeError('Overlay digest mismatch: '+entry['path'])
 dest=sdk/entry['path'];dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
for src in (root/'compat/rex').rglob('*'):
 if src.is_file():
  dest=root/'tools/rexglue-0.10.0/win-amd64/include/rex'/src.relative_to(root/'compat/rex');dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(src,dest)
print('ReXGlue v0.10.0 source and reviewed overlay ready.')
