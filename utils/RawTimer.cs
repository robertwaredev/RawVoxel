using Godot;
using System;
using System.Diagnostics;

namespace RAWUtils
{
    public static class RawTimer
    {
        public enum AppendLine { Pre, Post, Both, Skip }
        public static void Time(Action action, AppendLine appendLine = AppendLine.Skip)
        {
            string actionClass = action.Method.DeclaringType.Name.ToString();
            string actionName = action.Method.Name;

            Stopwatch stopwatch = Stopwatch.StartNew();

            action();

            stopwatch.Stop();
            
            if (appendLine == AppendLine.Pre || appendLine == AppendLine.Both) { GD.Print(" "); Console.WriteLine(); }
            
            Console.WriteLine("~ " + actionClass + "." + actionName + "() completed in " + stopwatch.ElapsedTicks / 10000.0f + " ms.");
                     GD.Print("~ " + actionClass + "." + actionName + "() completed in " + stopwatch.ElapsedTicks / 10000.0f + " ms.");
            
            if (appendLine == AppendLine.Post || appendLine == AppendLine.Both) { GD.Print(" "); Console.WriteLine(); }
        }
    }
}