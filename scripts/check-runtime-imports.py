"""Reject a release host linked to a diagnostic runtime (no dependencies)."""
from pathlib import Path
import struct
import sys

def imports(path):
    data = Path(path).read_bytes()
    pe = struct.unpack_from('<I', data, 60)[0]
    optional = pe + 24
    if data[pe:pe+4] != b'PE\0\0' or struct.unpack_from('<H', data, optional)[0] != 0x20B:
        raise ValueError('Expected a PE32+ executable')
    count = struct.unpack_from('<H', data, pe + 6)[0]
    size = struct.unpack_from('<H', data, pe + 20)[0]
    sections = [struct.unpack_from('<IIII', data, optional + size + i * 40 + 8)
                for i in range(count)]
    def offset(rva):
        for virtual_size, address, raw_size, raw in sections:
            if address <= rva < address + max(virtual_size, raw_size):
                return raw + rva - address
        raise ValueError('Import address outside sections')
    rva = struct.unpack_from('<I', data, optional + 120)[0]
    cursor = offset(rva)
    while any(struct.unpack_from('<IIIII', data, cursor)):
        name = offset(struct.unpack_from('<I', data, cursor + 12)[0])
        yield data[name:data.index(b'\0', name)].decode('ascii').lower()
        cursor += 20

if __name__ == '__main__':
    names = list(imports(sys.argv[1]))
    if 'rexruntime.dll' not in names or any('rexruntime' in n and n != 'rexruntime.dll' for n in names):
        raise SystemExit('Release executable must import rexruntime.dll, not a diagnostic runtime: ' + ', '.join(names))
    print('PASS: native executable imports the normal rexruntime.dll.')
