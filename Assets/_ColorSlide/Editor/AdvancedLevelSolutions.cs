using System;
using ColorSlide.Board;
using ColorSlide.Gameplay;
using UnityEngine;

namespace ColorSlide.Editor
{
    /// <summary>Reproducible authoring witnesses, never used by gameplay or as input automation.</summary>
    public static class AdvancedLevelSolutions
    {
        public static PuzzleAction[] For(int index)
        {
            switch (index)
            {
                case 0: return new[]
                {
                    X("A",0,7), X("C",0,2), Y("C",0,-1,true), X("B",1,0), Y("P",2,7), X("K",2,6),
                    X("P",5,7), Y("K",2,9,true), Y("R",4,0), Y("P",5,1), X("P",7,1,true), Y("A",0,8),
                    Y("G",0,6), Y("B",1,5), X("B",2,5), Y("B",2,9,true), X("G",-1,6,true), Y("A",0,-1,true),
                    X("R",0,0), Y("R",0,6), X("R",-1,6,true)
                };
                case 1: return new[]
                {
                    X("B",1,0), X("C",0,2), X("P",1,5), Y("R",4,0), Y("D",4,2), X("D",7,2,true),
                    Y("P",1,4), X("K",2,6), Y("K",2,10,true), X("R",5,0), X("B",4,0), Y("B",4,9),
                    X("B",2,9), Y("B",2,10,true), Y("C",0,-1,true), Y("G",0,0), Y("P",1,1), X("P",2,1),
                    Y("P",2,0), X("P",4,0), Y("P",4,1), X("A",1,7), Y("G",0,6), X("G",-1,6,true),
                    X("A",0,7), Y("A",0,-1,true), Y("P",4,2), X("R",0,0), Y("P",4,1), X("P",8,1,true),
                    Y("R",0,6), X("R",-1,6,true)
                };
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
        public static int Verify(LevelData data, PuzzleAction[] actions)
        {
            var session = data.CreateSession(); session.Start(); var steps = 0;
            foreach (var action in actions)
            {
                var move = session.MoveTowards(action.Block, action.Target, action.Axis);
                if (move.Status != (action.Exit ? MovementStatus.Exited : MovementStatus.Moved) ||
                    (!action.Exit && session.Board.GetBlock(action.Block).Anchor != action.Target))
                    throw new InvalidOperationException(data.Id + ": illegal witness action " + action);
                steps += move.AcceptedSteps;
                if (action.Exit) steps += Math.Abs(move.Exit.EndAnchor.x - move.Exit.StartAnchor.x) +
                    Math.Abs(move.Exit.EndAnchor.y - move.Exit.StartAnchor.y);
            }
            if (session.State != GameplayState.Won) throw new InvalidOperationException(data.Id + ": witness does not solve the level.");
            return steps;
        }
        private static PuzzleAction X(string id, int x, int y, bool exit = false) => new PuzzleAction(id, new Vector2Int(x,y), GridAxis.X, exit);
        private static PuzzleAction Y(string id, int x, int y, bool exit = false) => new PuzzleAction(id, new Vector2Int(x,y), GridAxis.Y, exit);
    }
}
