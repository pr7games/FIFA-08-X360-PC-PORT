// Xbox texture layout adapted from ReXGlue / Xenia, BSD-3-Clause.
// Copyright (c) 2022 Ben Vanik and Xenia project contributors.
// Copyright (c) 2026 Tom Clay. See ../THIRD_PARTY_NOTICES.txt.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Pr7Editor {
public sealed class Texture {
 public byte[] Container;public int Section,Width,Height,Format,Endian,Mips,BufferStart,BufferSize,Pitch,Block,BasePitch,MipStart;public bool Tiled,Packed;
 public string Role="";
 public override string ToString(){return (Role.Length>0?L.T(Role)+" · ":"")+L.T("Textura")+" "+Section+" · "+Width+" × "+Height+" · "+FormatName+" · "+Mips+" mips";}
 public string FormatName {get{return Format==18?"BC1":Format==19?"BC2":Format==20?"BC3":Format==49?"BC5":Format==6?"BGRA8":"L8";}}
 public static List<Texture> Parse(byte[] b) {
  if(b.Length<172||Encoding.ASCII.GetString(b,1,3)!="RW4"||Encoding.ASCII.GetString(b,4,3)!="xb2"||b[12]!=1)throw new InvalidDataException("No es RX2 Xbox 360 compatible.");
  int n=Bytes.U32(b,32),dir=Bytes.U32(b,48),res=Bytes.U32(b,68);if(n>100000)throw new InvalidDataException("RX2 invalido.");Bytes.Check(b,dir,checked(n*24));var output=new List<Texture>();
  for(int i=0;i<n;i++){int d=dir+i*24;if(Bytes.U32(b,d+20)!=0x20003)continue;int q=Bytes.U32(b,d);Bytes.Check(b,q,56);uint a=Bytes.UInt(b,q+28),w1=Bytes.UInt(b,q+32),w2=Bytes.UInt(b,q+36),w5=Bytes.UInt(b,q+48);int fmt=(int)(w1&63);
   if(!new[]{18,19,20,49,6,2}.Contains(fmt))continue;
   if(((w5>>9)&3)!=1||(w2>>26)!=0)continue; // Single 2D surface only; never rewrite cubes/arrays.
   int bi=(int)(w1>>12);if(bi>=n||Bytes.U32(b,dir+bi*24+20)!=0x10031)throw new InvalidDataException("Buffer RX2 invalido.");int start=checked(res+Bytes.U32(b,dir+bi*24)),size=Bytes.U32(b,dir+bi*24+8);Bytes.Check(b,start,size);
   var t=new Texture{Container=b,Section=i,Width=(int)(w2&8191)+1,Height=(int)((w2>>13)&8191)+1,Format=fmt,Endian=(int)((w1>>6)&3),Mips=b[q+54],BufferStart=start,BufferSize=size,Tiled=(a>>31)!=0,Packed=((w5>>11)&1)!=0,BasePitch=(int)((a>>22)&511)*32,MipStart=checked((int)(w5>>12)*4096)};
   t.Block=fmt==6||fmt==2?1:4;t.Pitch=fmt==18?8:fmt==6?4:fmt==2?1:16;
   if(t.Width>4096||t.Height>4096||t.Mips<1||t.Mips>Log2(Math.Max(t.Width,t.Height))+1||t.BasePitch<t.Width)throw new InvalidDataException("Dimensiones/mips RX2 no compatibles.");
   // Validate every mip before making any of this surface editable.
   for(int level=0;level<t.Mips;level++)t.CheckLevel(level);
   output.Add(t);
  }return output;
 }
 static int Log2(int n){int r=0;while(n>1){n>>=1;r++;}return r;}
 static int Pow2(int n){int r=1;while(r<n)r<<=1;return r;}
 static int CeilLog2(int n){return Log2(Pow2(n));}
 int PackedLevel{get{return Packed?Math.Max(0,CeilLog2(Math.Min(Width,Height))-4):99;}}
 void PackedXY(int level,out int x,out int y){x=y=0;int p=PackedLevel;if(level<p)return;int lw=CeilLog2(Width),lh=CeilLog2(Height),s=level-p;
  if(s<3){if(lw>lh)y=16>>s;else x=16>>s;}
  else if(lw>lh)x=(1<<(lw-p))>>(s-2);else y=(1<<(lh-p))>>(s-2);
  x/=Block;y/=Block;
 }
 int LevelStride(int level){int w=Math.Max(1,Pow2(Width)>>level),h=Math.Max(1,Pow2(Height)>>level);int pitch=Bytes.Align((w+Block-1)/Block,32)*Pitch;if(!Tiled)pitch=Bytes.Align(pitch,256);return Bytes.Align(pitch*Bytes.Align((h+Block-1)/Block,32),4096);}
 void Layout(int level,out int start,out int rowPitch,out int ox,out int oy){int stored=Math.Min(level,PackedLevel);PackedXY(level,out ox,out oy);
  if(level==0){start=0;rowPitch=Bytes.Align((BasePitch+Block-1)/Block,32);}
  else{start=MipStart;for(int l=1;l<stored;l++)start=checked(start+LevelStride(l));rowPitch=Bytes.Align((Math.Max(1,Pow2(Width)>>stored)+Block-1)/Block,32);if(!Tiled)rowPitch=Bytes.Align(rowPitch*Pitch,256)/Pitch;}
 }
 public static int TiledOffset(int x,int y,int pitch,int bpp){pitch=Bytes.Align(pitch,32);int lg=Log2(bpp),macro=((x>>5)+(y>>5)*(pitch>>5))<<(lg+7),micro=((x&7)+((y&14)<<2))<<lg;int o=macro+((micro&~15)<<1)+(micro&15)+((y&1)<<4);return ((o&~511)<<3)+((y&16)<<7)+((o&448)<<2)+(((((y&8)>>2)+(x>>3))&3)<<6)+(o&63);}
 void CheckLevel(int level){int start,p,ox,oy;Layout(level,out start,out p,out ox,out oy);int bw=(Math.Max(1,Width>>level)+Block-1)/Block,bh=(Math.Max(1,Height>>level)+Block-1)/Block;for(int y=0;y<bh;y++)for(int x=0;x<bw;x++){int a=checked(start+(Tiled?TiledOffset(x+ox,y+oy,p,Pitch):((y+oy)*p+x+ox)*Pitch));if(a<0||a>BufferSize-Pitch)throw new InvalidDataException("Mip fuera del buffer; superficie protegida.");}}
 void Swap(byte[] b,int p){if(Endian==1){for(int i=0;i<Pitch;i+=2){byte a=b[p+i];b[p+i]=b[p+i+1];b[p+i+1]=a;}}else if(Endian==2){for(int i=0;i<Pitch;i+=4)Array.Reverse(b,p+i,4);}else if(Endian==3){for(int i=0;i<Pitch;i+=4){byte a=b[p+i],c=b[p+i+1];b[p+i]=b[p+i+2];b[p+i+1]=b[p+i+3];b[p+i+2]=a;b[p+i+3]=c;}}}
 public byte[] ReadLevel(int level){int start,p,ox,oy;Layout(level,out start,out p,out ox,out oy);int bw=(Math.Max(1,Width>>level)+Block-1)/Block,bh=(Math.Max(1,Height>>level)+Block-1)/Block;byte[] r=new byte[bw*bh*Pitch];for(int y=0;y<bh;y++)for(int x=0;x<bw;x++){int a=BufferStart+start+(Tiled?TiledOffset(x+ox,y+oy,p,Pitch):((y+oy)*p+x+ox)*Pitch),d=(y*bw+x)*Pitch;Buffer.BlockCopy(Container,a,r,d,Pitch);Swap(r,d);}return r;}
 public void WriteLevel(int level,byte[] data){int start,p,ox,oy;Layout(level,out start,out p,out ox,out oy);int bw=(Math.Max(1,Width>>level)+Block-1)/Block,bh=(Math.Max(1,Height>>level)+Block-1)/Block;if(data.Length!=bw*bh*Pitch)throw new InvalidDataException("Mip de tamano incorrecto.");byte[] copy=(byte[])data.Clone();for(int y=0;y<bh;y++)for(int x=0;x<bw;x++){int s=(y*bw+x)*Pitch,a=BufferStart+start+(Tiled?TiledOffset(x+ox,y+oy,p,Pitch):((y+oy)*p+x+ox)*Pitch);Swap(copy,s);Buffer.BlockCopy(copy,s,Container,a,Pitch);}}
 public byte[] Dds(){var h=new byte[128];Encoding.ASCII.GetBytes("DDS ").CopyTo(h,0);Action<int,int> w=(p,n)=>Buffer.BlockCopy(BitConverter.GetBytes(n),0,h,p,4);w(4,124);w(8,Block==4?0xa1007:0x2100f);w(12,Height);w(16,Width);w(20,Block==4?ReadLevel(0).Length:Width*Pitch);w(28,Mips);w(76,32);
  if(Block==4){w(80,4);Encoding.ASCII.GetBytes(Format==18?"DXT1":Format==19?"DXT3":Format==20?"DXT5":"ATI2").CopyTo(h,84);}
  else if(Format==6){w(80,0x41);w(88,32);w(92,0xff0000);w(96,0xff00);w(100,0xff);w(104,unchecked((int)0xff000000));}
  else{w(80,0x20000);w(88,8);w(92,255);}w(108,Mips>1?0x401008:0x1000);
  using(var m=new MemoryStream()){m.Write(h,0,h.Length);for(int i=0;i<Mips;i++){byte[] b=ReadLevel(i);m.Write(b,0,b.Length);}return m.ToArray();}
 }
 public void ImportDds(byte[] d){Bytes.Check(d,0,128);if(Encoding.ASCII.GetString(d,0,4)!="DDS "||BitConverter.ToInt32(d,4)!=124||BitConverter.ToInt32(d,12)!=Height||BitConverter.ToInt32(d,16)!=Width||Math.Max(1,BitConverter.ToInt32(d,28))!=Mips)throw new InvalidDataException("DDS: conserva dimensiones y la cadena completa de "+Mips+" mipmaps.");
  if(BitConverter.ToInt32(d,112)!=0||BitConverter.ToInt32(d,24)>1)throw new InvalidDataException("DDS: solo se admite una superficie 2D.");string four=Encoding.ASCII.GetString(d,84,4);if(Block==4&&four!=(Format==18?"DXT1":Format==19?"DXT3":Format==20?"DXT5":"ATI2")&&!(Format==49&&four=="BC5U"))throw new InvalidDataException("El DDS debe conservar el formato "+FormatName+" (sin cabecera DX10).");
  if(Block==1){int bits=BitConverter.ToInt32(d,88);if(bits!=(Format==6?32:8)||BitConverter.ToInt32(d,92)!=(Format==6?0xff0000:255)||(Format==6&&(BitConverter.ToInt32(d,96)!=0xff00||BitConverter.ToInt32(d,100)!=255||BitConverter.ToInt32(d,104)!=unchecked((int)0xff000000))))throw new InvalidDataException("DDS: canales/pixel format no coinciden.");}
  int p=128;var levels=new List<byte[]>();for(int l=0;l<Mips;l++){int n=ReadLevel(l).Length;levels.Add(Bytes.Slice(d,p,n));p+=n;}if(p!=d.Length)throw new InvalidDataException("DDS: superficies adicionales o datos sobrantes.");for(int l=0;l<Mips;l++)WriteLevel(l,levels[l]);
 }
 public Bitmap Preview(){return Codec.Decode(ReadLevel(0),Width,Height,Format);}
 public void ImportPng(string path){using(var source=new Bitmap(path)){if(source.Width!=Width||source.Height!=Height)throw new InvalidDataException("La imagen debe medir "+Width+" × "+Height+".");var levels=new List<byte[]>();for(int l=0;l<Mips;l++){int w=Math.Max(1,Width>>l),h=Math.Max(1,Height>>l);using(var bmp=new Bitmap(w,h,PixelFormat.Format32bppArgb)){using(var g=Graphics.FromImage(bmp)){g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;using(var attributes=new ImageAttributes()){attributes.SetWrapMode(WrapMode.TileFlipXY);g.DrawImage(source,new Rectangle(0,0,w,h),0,0,Width,Height,GraphicsUnit.Pixel,attributes);}}levels.Add(Codec.Encode(bmp,Format));}}for(int l=0;l<Mips;l++)WriteLevel(l,levels[l]);}}
}
public static class Codec {
 static byte[] Pixels(Bitmap b){var r=new Rectangle(0,0,b.Width,b.Height);using(var copy=b.Clone(r,PixelFormat.Format32bppArgb)){var d=copy.LockBits(r,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);try{var bytes=new byte[b.Width*b.Height*4];for(int y=0;y<b.Height;y++)Marshal.Copy(IntPtr.Add(d.Scan0,y*d.Stride),bytes,y*b.Width*4,b.Width*4);return bytes;}finally{copy.UnlockBits(d);}}}
 static int[] RGB(int c){return new[]{((c>>11)&31)*255/31,((c>>5)&63)*255/63,(c&31)*255/31,255};}
 static int Pack(int r,int g,int b){return ((r*31+127)/255<<11)|((g*63+127)/255<<5)|((b*31+127)/255);}
 static int[][] Palette(int a,int b,bool alpha){int[][] c=new int[4][];c[0]=RGB(a);c[1]=RGB(b);if(a>b||!alpha){c[2]=new int[4];c[3]=new int[4];for(int j=0;j<3;j++){c[2][j]=(2*c[0][j]+c[1][j])/3;c[3][j]=(c[0][j]+2*c[1][j])/3;}c[2][3]=c[3][3]=255;}else{c[2]=new[]{(c[0][0]+c[1][0])/2,(c[0][1]+c[1][1])/2,(c[0][2]+c[1][2])/2,255};c[3]=new int[4];}return c;}
 static int[] AlphaPalette(byte a,byte b){int[] v=new int[8];v[0]=a;v[1]=b;if(a>b)for(int i=1;i<=6;i++)v[i+1]=((7-i)*a+i*b)/7;else{for(int i=1;i<=4;i++)v[i+1]=((5-i)*a+i*b)/5;v[6]=0;v[7]=255;}return v;}
 static int[] ReadAlpha(byte[] b,int p){var pal=AlphaPalette(b[p],b[p+1]);ulong bits=0;for(int i=0;i<6;i++)bits|=(ulong)b[p+2+i]<<(8*i);var a=new int[16];for(int i=0;i<16;i++)a[i]=pal[(bits>>(3*i))&7];return a;}
 static void WriteAlpha(byte[] b,int p,int[] values){byte hi=(byte)values.Max(),lo=(byte)values.Min();b[p]=hi;b[p+1]=lo;var pal=AlphaPalette(hi,lo);ulong bits=0;for(int i=0;i<16;i++){int best=0,error=Int32.MaxValue;for(int j=0;j<8;j++){int e=Math.Abs(values[i]-pal[j]);if(e<error){error=e;best=j;}}bits|=(ulong)best<<(3*i);}for(int i=0;i<6;i++)b[p+2+i]=(byte)(bits>>(8*i));}
 public static Bitmap Decode(byte[] data,int w,int h,int fmt){byte[] pixels=new byte[w*h*4];int pitch=fmt==18?8:16,bw=(w+3)/4;
  if(fmt==6)Buffer.BlockCopy(data,0,pixels,0,pixels.Length);
  else if(fmt==2){for(int i=0;i<w*h;i++){pixels[i*4]=pixels[i*4+1]=pixels[i*4+2]=data[i];pixels[i*4+3]=255;}}
  else for(int by=0;by<(h+3)/4;by++)for(int bx=0;bx<bw;bx++){int p=(by*bw+bx)*pitch;int[][] colors=null;uint bits=0;int[] a=null,red=null,green=null;
   if(fmt==49){red=ReadAlpha(data,p);green=ReadAlpha(data,p+8);}else{int q=p+(fmt==18?0:8);int c0=data[q]|data[q+1]<<8,c1=data[q+2]|data[q+3]<<8;colors=Palette(c0,c1,fmt==18);bits=BitConverter.ToUInt32(data,q+4);if(fmt==20)a=ReadAlpha(data,p);}
   for(int i=0;i<16;i++){int x=bx*4+i%4,y=by*4+i/4;if(x>=w||y>=h)continue;int k=(y*w+x)*4;
    if(fmt==49){int r=red[i],g=green[i];double nx=r/127.5-1,ny=g/127.5-1;pixels[k]=(byte)(Math.Sqrt(Math.Max(0,1-nx*nx-ny*ny))*127.5+127.5);pixels[k+1]=(byte)g;pixels[k+2]=(byte)r;pixels[k+3]=255;}
    else{var c=colors[(bits>>(2*i))&3];pixels[k]=(byte)c[2];pixels[k+1]=(byte)c[1];pixels[k+2]=(byte)c[0];int alpha=c[3];if(fmt==19)alpha=((data[p+i/2]>>(4*(i%2)))&15)*17;else if(fmt==20)alpha=a[i];pixels[k+3]=(byte)alpha;}
   }
  }
  var b=new Bitmap(w,h,PixelFormat.Format32bppArgb);var d=b.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);try{for(int y=0;y<h;y++)Marshal.Copy(pixels,y*w*4,IntPtr.Add(d.Scan0,y*d.Stride),w*4);}finally{b.UnlockBits(d);}return b;
 }
 public static byte[] Encode(Bitmap bitmap,int fmt){byte[] pixels=Pixels(bitmap);int w=bitmap.Width,h=bitmap.Height;if(fmt==6)return pixels;if(fmt==2){var l=new byte[w*h];for(int i=0;i<l.Length;i++)l[i]=pixels[i*4+2];return l;}
  int bw=(w+3)/4,pitch=fmt==18?8:16;var result=new byte[bw*((h+3)/4)*pitch];
  for(int by=0;by<(h+3)/4;by++)for(int bx=0;bx<bw;bx++){int p=(by*bw+bx)*pitch;int[][] cs=new int[16][];var alpha=new int[16];for(int i=0;i<16;i++){int x=Math.Min(w-1,bx*4+i%4),y=Math.Min(h-1,by*4+i/4),k=(y*w+x)*4;cs[i]=new[]{(int)pixels[k+2],(int)pixels[k+1],(int)pixels[k],(int)pixels[k+3]};alpha[i]=pixels[k+3];}
   if(fmt==49){WriteAlpha(result,p,cs.Select(c=>c[0]).ToArray());WriteAlpha(result,p+8,cs.Select(c=>c[1]).ToArray());continue;}
   if(fmt==19)for(int i=0;i<16;i++)result[p+i/2]|=(byte)(((alpha[i]*15+127)/255)<<(4*(i%2)));else if(fmt==20)WriteAlpha(result,p,alpha);
   bool transparent=fmt==18&&alpha.Any(v=>v<128);int[] min={255,255,255},max={0,0,0};for(int i=0;i<16;i++)if(!transparent||alpha[i]>=128)for(int c=0;c<3;c++){min[c]=Math.Min(min[c],cs[i][c]);max[c]=Math.Max(max[c],cs[i][c]);}
   int c0=Pack(max[0],max[1],max[2]),c1=Pack(min[0],min[1],min[2]);if(transparent){int t=c0;c0=c1;c1=t;}else if(c0<=c1){if(c1<65535)c0=c1+1;else c1=c0-1;}
   if(transparent&&alpha.All(v=>v<128))c0=c1=0;var palette=Palette(c0,c1,transparent);uint mask=0;for(int i=0;i<16;i++){int best=0,error=Int32.MaxValue;if(transparent&&alpha[i]<128)best=3;else for(int j=0;j<(transparent?3:4);j++){int e=0;for(int c=0;c<3;c++){int d=cs[i][c]-palette[j][c];e+=d*d;}if(e<error){error=e;best=j;}}mask|=(uint)best<<(2*i);}
   int q=p+(fmt==18?0:8);result[q]=(byte)c0;result[q+1]=(byte)(c0>>8);result[q+2]=(byte)c1;result[q+3]=(byte)(c1>>8);Buffer.BlockCopy(BitConverter.GetBytes(mask),0,result,q+4,4);
  }return result;
 }
}
}
