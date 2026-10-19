using System;

namespace ColorSlide.Editor
{
    /// <summary>Production-rule replayed, locally shortened authoring witnesses. Not optimality claims.</summary>
    public static class MasterLevelSolutions
    {
        public static PuzzleAction[] For(int index)
        {
            switch (index)
            {
                case 0: return new[]
                {
                    X("B",1,0), X("C",0,2), X("P",1,5), Y("F",4,0), Y("P",1,4), X("K",2,6), Y("K",2,11,true),
                    X("F",2,0), Y("R",4,0), Y("D",4,2), X("D",7,2,true), X("R",6,0), Y("E",4,1), Y("F",2,1),
                    X("B",4,0), Y("C",0,-1,true), X("F",0,1), Y("P",1,5), Y("G",0,4), Y("F",0,2), X("E",0,1),
                    Y("B",4,10), X("B",2,10), Y("B",2,11,true), Y("E",0,-1,true), X("R",0,0), X("A",1,7),
                    X("P",2,5), Y("G",0,6), X("G",-1,6,true), Y("F",0,6), X("F",-1,6,true), Y("R",0,6),
                    X("R",-1,6,true), X("A",0,7), Y("A",0,-1,true), X("P",1,5), Y("P",1,0), X("P",4,0),
                    Y("P",4,1), X("P",8,1,true)
                };
                case 1: return new[]
                {
                    X("P",1,5), Y("F",4,0), X("H",1,0), Y("B",0,1), X("C",0,2), Y("P",1,4), X("K",2,6),
                    Y("K",2,11,true), X("F",2,0), Y("R",4,0), Y("D",4,2), X("D",7,2,true), X("R",6,0),
                    Y("I",4,2), X("I",6,2), Y("E",4,0), Y("P",1,5), Y("G",0,4), Y("C",0,3), Y("F",2,1),
                    Y("H",1,1), X("E",0,0), Y("E",0,-1,true), Y("B",0,0), X("B",4,0), Y("B",4,10), X("B",2,10),
                    Y("B",2,11,true), Y("F",2,0), X("F",3,0), X("H",2,1), Y("C",0,-1,true), Y("G",0,0),
                    X("H",1,1), X("P",0,5), Y("H",1,5), X("H",2,5), Y("H",2,10,true), X("P",1,5), Y("G",0,4),
                    X("F",0,0), X("I",4,2), Y("I",4,0), Y("F",0,2), X("I",0,0), Y("I",0,-1,true), X("R",0,0),
                    X("A",1,7), X("P",2,5), Y("G",0,6), X("G",-1,6,true), Y("F",0,6), X("F",-1,6,true),
                    X("P",1,5), Y("P",1,1), X("P",2,1), Y("P",2,0), X("P",4,0), Y("P",4,1), X("P",8,1,true),
                    Y("R",0,6), X("R",-1,6,true), X("A",0,7), Y("A",0,-1,true)
                };
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
        private static PuzzleAction X(string id, int x, int y, bool exit = false) => new PuzzleAction(id,new UnityEngine.Vector2Int(x,y),ColorSlide.Board.GridAxis.X,exit);
        private static PuzzleAction Y(string id, int x, int y, bool exit = false) => new PuzzleAction(id,new UnityEngine.Vector2Int(x,y),ColorSlide.Board.GridAxis.Y,exit);
    }
}
