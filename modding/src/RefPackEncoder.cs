using System;
using System.IO;

namespace Pr7Editor {
// RefPack 10FB, 24-bit expanded size; all three back-reference forms.
public static class RefPackEncoder {
 static int Hash(byte[] b,int p){return ((b[p]*251+b[p+1])*251+b[p+2])&131071;}
 public static byte[] Pack(byte[] b){
  if(b.Length>0xffffff)throw new InvalidDataException("Recurso demasiado grande para RefPack.");
  int[] head=new int[131072],prev=new int[b.Length];for(int i=0;i<head.Length;i++)head[i]=-1;
  using(var m=new MemoryStream()){
   m.WriteByte(0x10);m.WriteByte(0xfb);m.WriteByte((byte)(b.Length>>16));m.WriteByte((byte)(b.Length>>8));m.WriteByte((byte)b.Length);
   int p=0,pending=0;
   while(p+2<b.Length){int h=Hash(b,p),candidate=head[h],best=0,distance=0,bestGain=0,limit=Math.Min(1028,b.Length-p),budget=1024;
    while(candidate>=0&&p-candidate<=131072&&budget-->0){int d=p-candidate;
     if(b[candidate]==b[p]&&b[candidate+1]==b[p+1]&&b[candidate+2]==b[p+2]){
      int n=3;while(n<limit&&b[candidate+n]==b[p+n])n++;
      int length=0,cost=0;if(d<=1024&&n>=3){length=Math.Min(10,n);cost=2;}
      if(d<=16384&&n>=4&&Math.Min(67,n)-3>length-cost){length=Math.Min(67,n);cost=3;}
      if(n>=5&&n-4>length-cost){length=n;cost=4;}
      if(length-cost>bestGain){bestGain=length-cost;best=length;distance=d;if(best==limit)break;}
     }candidate=prev[candidate];
    }
    if(bestGain>0){
     int literals=p-pending;while(literals>=4){int n=Math.Min(112,literals&~3);m.WriteByte((byte)(0xe0+(n-4)/4));m.Write(b,pending,n);pending+=n;literals-=n;}
     int off=distance-1;
     if(best<=10&&distance<=1024){m.WriteByte((byte)(((off>>8)<<5)|((best-3)<<2)|literals));m.WriteByte((byte)off);}
     else if(best<=67&&distance<=16384){m.WriteByte((byte)(0x80|(best-4)));m.WriteByte((byte)((literals<<6)|(off>>8)));m.WriteByte((byte)off);}
     else{int n=best-5;m.WriteByte((byte)(0xc0|((off>>16)<<4)|((n>>8)<<2)|literals));m.WriteByte((byte)(off>>8));m.WriteByte((byte)off);m.WriteByte((byte)n);}
     m.Write(b,pending,literals);int end=p+best;while(p<end){if(p+2<b.Length){h=Hash(b,p);prev[p]=head[h];head[h]=p;}p++;}pending=p;
    }else{prev[p]=head[h];head[h]=p;p++;}
   }
   int left=b.Length-pending;while(left>=4){int n=Math.Min(112,left&~3);m.WriteByte((byte)(0xe0+(n-4)/4));m.Write(b,pending,n);pending+=n;left-=n;}
   m.WriteByte((byte)(0xfc+left));m.Write(b,pending,left);return m.ToArray();
  }
 }
}
}
