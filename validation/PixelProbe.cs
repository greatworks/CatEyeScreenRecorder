using System; using System.Drawing;
class X { static void Main(string[] a) { using(var b=new Bitmap(a[0])) { int x=int.Parse(a[1]); foreach(int y in new int[]{740,760,790,800,810,820,830,840,850,857,860,870,880,890,900,920,940}) Console.WriteLine(y+":"+b.GetPixel(x,y)); } } }
