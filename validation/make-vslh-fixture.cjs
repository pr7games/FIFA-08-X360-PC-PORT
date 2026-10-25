const fs = require('node:fs');
const path = require('node:path');
// PowerPC ELF: four vslh/blr functions, with destination/source aliases.
const b = Buffer.alloc(0x280);
const u16 = (o, n) => b.writeUInt16BE(n, o);
const u32 = (o, n) => b.writeUInt32BE(n >>> 0, o);
b.set([0x7f, 0x45, 0x4c, 0x46, 1, 2, 1]);
u16(16, 2); u16(18, 20); u32(20, 1); u32(24, 0x1000);
u32(28, 52); u32(32, 0x200); u16(40, 52); u16(42, 32);
u16(44, 1); u16(46, 40); u16(48, 3); u16(50, 2);
u32(52, 1); u32(56, 0x100); u32(60, 0x1000); u32(64, 0x1000);
u32(68, 32); u32(72, 32); u32(76, 5); u32(80, 4);
[[1,2,3], [2,2,3], [3,2,3], [1,2,2]].forEach(([d,a,c],i) => {
  u32(0x100+i*8, (4 << 26) | (d << 21) | (a << 16) | (c << 11) | 324);
  u32(0x104+i*8, 0x4e800020);
});
b.write('\0.text\0.shstrtab\0', 0x140, 'ascii');
u32(0x228, 1); u32(0x22c, 1); u32(0x230, 6); u32(0x234, 0x1000);
u32(0x238, 0x100); u32(0x23c, 32); u32(0x248, 4);
u32(0x250, 7); u32(0x254, 3); u32(0x260, 0x140); u32(0x264, 17); u32(0x270, 1);
fs.mkdirSync(path.join(__dirname,'input'), {recursive:true});
fs.mkdirSync(path.join(__dirname,'generated'), {recursive:true});
fs.writeFileSync(path.join(__dirname,'input','vslh.o'),b);
