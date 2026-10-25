"""Fetch the exact gitlink revisions from ReXGlue v0.10.0 (source archives)."""
import concurrent.futures
import io
import json
import pathlib
import re
import shutil
import urllib.request
import zipfile

root = pathlib.Path(__file__).resolve().parent / 'rexglue-source/rexglue-sdk-0.10.0'
headers = {'User-Agent': 'FIFA08-Research-build'}
def download(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=headers), timeout=120) as response:
        return response.read()
expected_root = root / 'thirdparty/tl-expected'
for relative, remote in [('include/tl/expected.hpp', 'include/tl/expected.hpp'), ('COPYING', 'COPYING')]:
    destination = expected_root / relative
    if not destination.exists():
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(download(f'https://raw.githubusercontent.com/TartanLlama/expected/v1.1.0/{remote}'))
directx_root = root.parents[1] / 'directx-headers'
if not (directx_root / 'DirectX-Headers-1.614.1/include/directx/d3d12.h').exists():
    archive = zipfile.ZipFile(io.BytesIO(download('https://codeload.github.com/microsoft/DirectX-Headers/zip/refs/tags/v1.614.1')))
    for member in archive.infolist():
        parts = pathlib.PurePosixPath(member.filename).parts
        if '..' in parts:
            raise ValueError('Unexpected DirectX archive path')
        destination = directx_root.joinpath(*parts)
        if member.is_dir():
            destination.mkdir(parents=True, exist_ok=True)
        else:
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(archive.read(member))
tree = json.loads(download('https://api.github.com/repos/rexglue/rexglue-sdk/git/trees/v0.10.0?recursive=1'))
revisions = {item['path']: item['sha'] for item in tree['tree'] if item['type'] == 'commit'}
modules = re.findall(r'path = ([^\r\n]+)\s+url = https://github.com/([^\r\n]+)', (root / '.gitmodules').read_text())
selected = {'cli11','libmspack','FFmpeg','tomlplusplus','simde','xxHash','spdlog','fmt','utfcpp','imgui','sdl3','vulkan-headers','vulkan-memory-allocator','spirv-headers','spirv-tools','glslang','tracy','o1heap','inja'}
def fetch(module):
    relative, repository = module
    if relative.split('/')[-1] not in selected:
        return None
    revision = revisions[relative]
    destination = root / relative
    marker = destination / '.archive-revision'
    if marker.exists() and marker.read_text() == revision:
        return {'path': relative, 'repository': repository, 'revision': revision}
    repository = repository.removesuffix('.git')
    url = f'https://codeload.github.com/{repository}/zip/{revision}'
    archive = zipfile.ZipFile(io.BytesIO(download(url)))
    for member in archive.infolist():
        parts = pathlib.PurePosixPath(member.filename).parts[1:]
        if not parts or '..' in parts:
            continue
        target = destination.joinpath(*parts)
        if member.is_dir():
            target.mkdir(parents=True, exist_ok=True)
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(archive.read(member))
    marker.write_text(revision)
    print(f'Prepared {relative} @ {revision[:12]}', flush=True)
    return {'path': relative, 'repository': repository, 'revision': revision, 'url': url}
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as executor:
    manifest = [item for item in executor.map(fetch, modules) if item]
(root / 'dependency-revisions.json').write_text(json.dumps(manifest, indent=2))
# GitHub ZIPs represent symlinks as tiny text files on Windows. Materialize
# their file targets without requiring administrator-created symlinks.
for item in manifest:
    dependency = root / item['path']
    for target in dependency.rglob('*'):
        if target.is_file() and target.stat().st_size < 256:
            try:
                link = target.read_text().strip()
            except UnicodeError:
                continue
            if not re.fullmatch(r'(?:\.\./)+[\w./-]+', link):
                continue
            source = (target.parent / link).resolve()
            if source.is_relative_to(dependency.resolve()) and source.is_file():
                shutil.copyfile(source, target)
                print(f'Materialized source link: {target.relative_to(root)}', flush=True)
