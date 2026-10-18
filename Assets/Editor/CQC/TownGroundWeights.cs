#if UNITY_EDITOR
using System;
namespace StarterAssets.CQCEditorTools
{
 // Terrain alphamaps are stored with limited precision. Normalize the existing
 // sample before blending; leave samples outside the paint mask byte-for-byte.
 public static class TownGroundWeights
 {
  public static void Blend(float[,,] previous,float[,,] output,int z,int x,int grass,int dirt,float brown,float blend)
  {
   int count=output.GetLength(2),oldCount=previous.GetLength(2);
   if(blend<=0){for(int l=0;l<count;l++)output[z,x,l]=l<oldCount?previous[z,x,l]:0;return;}
   blend=Clamp(blend);brown=Clamp(brown);double oldSum=0;
   for(int l=0;l<count;l++)if(l<oldCount)oldSum+=Clamp(previous[z,x,l]);
   double sum=0;
   for(int l=0;l<count;l++)
   {
    double source=oldSum>1e-12?(l<oldCount?Clamp(previous[z,x,l])/oldSum:0):(l==grass?1:0);
    double target=l==grass?1-brown:l==dirt?brown:0;
    float value=(float)(source*(1-blend)+target*blend);output[z,x,l]=value;sum+=value;
   }
   for(int l=0;l<count;l++)output[z,x,l]=(float)(output[z,x,l]/sum);
  }
  static float Clamp(float value)=>float.IsNaN(value)||float.IsInfinity(value)?0:Math.Max(0,Math.Min(1,value));
 }
}
#endif
