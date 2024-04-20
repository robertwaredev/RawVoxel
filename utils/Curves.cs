using Godot;

namespace RawUtils
{
    public static class Curves
    {
        private static void CreatePoint(Curve curve, int index, float x, float y, Curve.TangentMode mode)
        {
            curve.AddPoint(new Vector2(x, y));
            curve.SetPointRightMode(index, mode);
        }
        
        private static void CreatePoints(Curve curve, Curve.TangentMode mode, int count)
        {
            float curveRange = Mathf.Abs(curve.MaxValue) + Mathf.Abs(curve.MinValue);
            
            for (int i = 0; i < count; i ++)
            {
                CreatePoint(curve, i, i / curveRange , i / curveRange, mode);
            }
        }
        
        private static void SetPoint(Curve curve, int index, float x, float y, Curve.TangentMode mode)
        {
            curve.SetPointOffset(index, x);
            curve.SetPointValue(index, y);
            curve.SetPointRightMode(index, mode);
        }
        
        private static void SetPoints(Curve curve, Curve.TangentMode mode)
        {
            for (int i = 0; i < curve.PointCount; i ++)
            {
                SetPoint(curve, i, i / curve.MaxValue , i / curve.MaxValue, mode);
            }
        }

        public static void NewLinear(Curve curve, int count = 2)
        {
            CreatePoints(curve, Curve.TangentMode.Linear, count);
        }

        public static void NewSmooth(Curve curve, int count = 2)
        {
            CreatePoints(curve, Curve.TangentMode.Free, count);
        }

        public static void SetLinear(Curve curve, int count = 2)
        {
            CreatePoints(curve, Curve.TangentMode.Linear, count);
        }

        public static void SetSmooth(Curve curve, int count = 2)
        {
            CreatePoints(curve, Curve.TangentMode.Free, count);
        }        

        public static void Snap(Curve curve, int snapSize = 2)
        {
            if (curve.PointCount < snapSize)
            {
                for (int i = 0; i < snapSize - curve.PointCount; i ++)
                {

                }
            }
        }
    }
}