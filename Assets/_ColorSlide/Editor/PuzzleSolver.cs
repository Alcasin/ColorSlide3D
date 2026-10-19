using System;
using System.Collections.Generic;
using System.Linq;
using ColorSlide.Board;
using ColorSlide.Gameplay;
using UnityEngine;

namespace ColorSlide.Editor
{
    public readonly struct PuzzleAction
    {
        public readonly string Block;
        public readonly Vector2Int Target;
        public readonly GridAxis Axis;
        public readonly bool Exit;
        public PuzzleAction(string block, Vector2Int target, GridAxis axis, bool exit)
        { Block = block; Target = target; Axis = axis; Exit = exit; }
        public override string ToString() => Block + " " + Axis + " -> (" + Target.x + "," + Target.y + ")" + (Exit ? " exit" : "");
    }

    /// <summary>
    /// Bounded, deterministic authoring search. One action is any positive straight slide to a legal
    /// anchor, or a straight slide through a matching exit. It is NOT a pointer-down gesture (which
    /// can change axis). Successors call GridBoard.MoveWithExits, including its swept-footprint check.
    /// Weight 1 proves a shortest action count if solved; higher weights only produce witnesses.
    /// Never runs automatically on import, Play, or in ordinary tests.
    /// </summary>
    public static class PuzzleSolver
    {
        /// <summary>Authoring-only shortening: remove one or two unnecessary actions, replaying the entire
        /// candidate through PuzzleSession each time. Does not establish global optimality.</summary>
        public static PuzzleAction[] SimplifyWitness(LevelData level, IEnumerable<PuzzleAction> witness)
        {
            var actions = witness.ToArray(); AdvancedLevelSolutions.Verify(level, actions);
            while (true)
            {
                var shortened = false;
                for (var first = 0; first < actions.Length && !shortened; first++)
                for (var second = first; second < actions.Length && !shortened; second++)
                {
                    if (actions[first].Exit || actions[second].Exit) continue;
                    var candidate = actions.Where((_, index) => index != first && index != second).ToArray();
                    try { AdvancedLevelSolutions.Verify(level, candidate); }
                    catch (InvalidOperationException) { continue; }
                    actions = candidate; shortened = true;
                }
                if (!shortened) return actions;
            }
        }

        public sealed class Result
        {
            public PuzzleAction[] Actions = Array.Empty<PuzzleAction>();
            public bool Solved, Optimal;
            public int Expanded, Discovered, InitialLowerBound, ProvenLowerBound;
            public string Stop;
        }
        private sealed class Node
        {
            public string Key;
            public int Cost, Parent;
            public PuzzleAction Action;
        }
        private readonly struct Entry
        {
            public readonly int Node, Score, Heuristic, Order;
            public Entry(int node, int score, int heuristic, int order)
            { Node = node; Score = score; Heuristic = heuristic; Order = order; }
        }
        // Stable binary min heap: score, then lower heuristic, then insertion order.
        private sealed class Frontier
        {
            private readonly List<Entry> items = new List<Entry>();
            public int Count => items.Count;
            private static bool Less(Entry a, Entry b) => a.Score != b.Score ? a.Score < b.Score :
                a.Heuristic != b.Heuristic ? a.Heuristic < b.Heuristic : a.Order < b.Order;
            public void Push(Entry entry)
            {
                items.Add(entry); var index = items.Count - 1;
                while (index > 0)
                {
                    var parent = (index - 1) / 2; if (!Less(entry, items[parent])) break;
                    items[index] = items[parent]; index = parent;
                }
                items[index] = entry;
            }
            public Entry Pop()
            {
                var result = items[0]; var tail = items[items.Count - 1]; items.RemoveAt(items.Count - 1);
                if (items.Count == 0) return result;
                var index = 0;
                while (index * 2 + 1 < items.Count)
                {
                    var child = index * 2 + 1;
                    if (child + 1 < items.Count && Less(items[child + 1], items[child])) child++;
                    if (!Less(items[child], tail)) break;
                    items[index] = items[child]; index = child;
                }
                items[index] = tail; return result;
            }
        }

        public static Result Search(LevelData level, int maxExpanded = 100000, int maxDiscovered = 300000, int weight = 1)
        {
            if (maxExpanded < 1 || maxDiscovered < 1 || weight < 1) throw new ArgumentOutOfRangeException(nameof(maxExpanded));
            if ((long)level.Width * level.Height >= char.MaxValue) throw new ArgumentException("Search state encoding needs fewer than 65535 cells.");
            level.CreateSession();
            var lower = SoloDistances(level);
            var start = new string(level.Blocks.Select(block => Encode(block.Anchor, level.Width)).ToArray());
            var result = new Result { InitialLowerBound = Heuristic(start, lower) };
            result.ProvenLowerBound = result.InitialLowerBound;
            var nodes = new List<Node> { new Node { Key = start, Parent = -1 } };
            var best = new Dictionary<string, int>(StringComparer.Ordinal) { [start] = 0 };
            var open = new Frontier(); var sequence = 0;
            open.Push(new Entry(0, weight * result.InitialLowerBound, result.InitialLowerBound, sequence++));
            while (open.Count > 0)
            {
                var entry = open.Pop(); var node = nodes[entry.Node];
                if (best[node.Key] != entry.Node) continue;
                if (weight == 1) result.ProvenLowerBound = entry.Score;
                if (node.Key.All(cell => cell == 0))
                {
                    var actions = new List<PuzzleAction>(); var index = entry.Node;
                    while (nodes[index].Parent >= 0) { actions.Add(nodes[index].Action); index = nodes[index].Parent; }
                    actions.Reverse(); result.Actions = actions.ToArray(); result.Solved = true; result.Optimal = weight == 1;
                    result.Stop = "Solved"; break;
                }
                if (result.Expanded >= maxExpanded || best.Count >= maxDiscovered)
                { result.Stop = "Search budget reached"; break; }
                result.Expanded++;
                var board = Restore(level, node.Key); var chars = node.Key.ToCharArray();
                for (var blockIndex = 0; blockIndex < level.Blocks.Length; blockIndex++)
                {
                    if (chars[blockIndex] == 0) continue;
                    var definition = level.Blocks[blockIndex]; var origin = Decode(chars[blockIndex], level.Width);
                    foreach (var direction in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up })
                    {
                        var axis = direction.x == 0 ? GridAxis.Y : GridAxis.X;
                        while (true)
                        {
                            var target = board.GetBlock(definition.Id).Anchor + direction;
                            var move = board.MoveWithExits(definition.Id, target, axis);
                            if (move.Status != MovementStatus.Moved && move.Status != MovementStatus.Exited) break;
                            var exited = move.Status == MovementStatus.Exited;
                            chars[blockIndex] = exited ? (char)0 : Encode(target, level.Width);
                            var key = new string(chars); var cost = node.Cost + 1;
                            if (!best.TryGetValue(key, out var old) || cost < nodes[old].Cost)
                            {
                                var h = Heuristic(key, lower);
                                if (h < 1000000)
                                {
                                    if (best.Count >= maxDiscovered)
                                    { result.Stop = "Search budget reached"; goto Finished; }
                                    var next = nodes.Count;
                                    nodes.Add(new Node { Key = key, Cost = cost, Parent = entry.Node,
                                        Action = new PuzzleAction(definition.Id, target, axis, exited) });
                                    best[key] = next; open.Push(new Entry(next, cost + weight * h, h, sequence++));
                                }
                            }
                            if (exited) break;
                        }
                        // Restore the SAME production board before exploring another direction/block.
                        if (!board.IsActive(definition.Id)) board.AddBlock(definition.Id, origin, definition.Cells, definition.Color);
                        else while (board.GetBlock(definition.Id).Anchor != origin)
                            if (!board.TryMoveStep(definition.Id, -direction)) throw new InvalidOperationException("Search undo failed.");
                        chars[blockIndex] = Encode(origin, level.Width);
                    }
                }
            }
        Finished:
            result.Discovered = best.Count;
            if (result.Stop == null) result.Stop = "Exhausted reachable states";
            return result;
        }

        private static GridBoard Restore(LevelData level, string state)
        {
            var board = new GridBoard(level.Width, level.Height, level.Obstacles);
            for (var i = 0; i < state.Length; i++) if (state[i] != 0)
            { var block = level.Blocks[i]; board.AddBlock(block.Id, Decode(state[i], level.Width), block.Cells, block.Color); }
            board.ConfigureGates(level.Gates.Select(gate => gate.CreateGate())); return board;
        }
        private static char Encode(Vector2Int cell, int width) => (char)(1 + cell.y * width + cell.x);
        private static Vector2Int Decode(char cell, int width) => new Vector2Int((cell - 1) % width, (cell - 1) / width);
        private static int Heuristic(string key, int[][] lower)
        { var sum = 0; for (var i = 0; i < key.Length; i++) sum += lower[i][key[i]]; return sum; }

        // Exact independent-piece distances, with obstacles and gates, but no other pieces.
        // Their sum is admissible: each real action moves only one piece. Dead solo anchors are pruned.
        private static int[][] SoloDistances(LevelData level)
        {
            var result = new int[level.Blocks.Length][];
            for (var i = 0; i < result.Length; i++)
            {
                var block = level.Blocks[i]; var count = level.Width * level.Height + 1;
                var reverse = Enumerable.Range(0, count).Select(_ => new List<int>()).ToArray();
                for (var cell = 1; cell < count; cell++)
                {
                    var origin = Decode((char)cell, level.Width); GridBoard board;
                    try { board = new GridBoard(level.Width, level.Height, level.Obstacles); board.AddBlock(block.Id, origin, block.Cells, block.Color); }
                    catch (InvalidOperationException) { continue; }
                    board.ConfigureGates(level.Gates.Select(gate => gate.CreateGate()));
                    foreach (var direction in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up })
                    {
                        while (true)
                        {
                            var target = board.GetBlock(block.Id).Anchor + direction;
                            var move = board.MoveWithExits(block.Id, target, direction.x == 0 ? GridAxis.Y : GridAxis.X);
                            if (move.Status != MovementStatus.Moved && move.Status != MovementStatus.Exited) break;
                            var next = move.Status == MovementStatus.Exited ? 0 : Encode(target, level.Width);
                            reverse[next].Add(cell); if (next == 0) break;
                        }
                        if (!board.IsActive(block.Id)) board.AddBlock(block.Id, origin, block.Cells, block.Color);
                        else while (board.GetBlock(block.Id).Anchor != origin) board.TryMoveStep(block.Id, -direction);
                    }
                }
                var distance = Enumerable.Repeat(1000000, count).ToArray(); distance[0] = 0;
                var queue = new Queue<int>(); queue.Enqueue(0);
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue();
                    foreach (var previous in reverse[cell]) if (distance[previous] == 1000000)
                    { distance[previous] = distance[cell] + 1; queue.Enqueue(previous); }
                }
                result[i] = distance;
            }
            return result;
        }
    }
}
