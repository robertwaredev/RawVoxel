using Godot;
using System;
using System.Diagnostics;

namespace RawUtils
{
    public static class RawTimer
    {
        public enum AppendLine { Pre, Post, Both, Skip }
        public static void Time(Action action, AppendLine appendLine = AppendLine.Skip)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            action();

            stopwatch.Stop();

            string actionClass = action.Method.DeclaringType.Name.ToString();
            string actionName = action.Method.Name;
            
            if (appendLine == AppendLine.Pre || appendLine == AppendLine.Both) { GD.Print(" "); Console.WriteLine(); }
            
            GD.Print("~~~ " + actionClass + "." + actionName + "() completed in " + stopwatch.ElapsedTicks / 10000.0f + " ms.");
            
            if (appendLine == AppendLine.Post || appendLine == AppendLine.Both) { GD.Print(" "); Console.WriteLine(); }
        }
    }
}