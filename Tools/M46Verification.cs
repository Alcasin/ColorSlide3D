using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using ColorSlide.Editor;
using ColorSlide.Gameplay;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Filters;

// Standalone authoring / validation entry point, deliberately outside Assets and player assemblies.
internal static class M46Verification
{
    private sealed class Listener : ITestListener
    {
        public void TestStarted(ITest test) { }
        public void TestOutput(TestOutput output) { }
        public void TestFinished(ITestResult result)
        {
            if (result.ResultState.Status == TestStatus.Failed)
                Console.WriteLine(result.FullName + "\n" + result.Message + "\n" + result.StackTrace);
        }
    }
    private static void Xml(TNode node, string path)
    { using (var writer = XmlWriter.Create(path,new XmlWriterSettings { Indent = true })) node.WriteTo(writer); }
    private static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var player = System.Reflection.Assembly.LoadFrom(Path.Combine(args[1],"ColorSlide.Runtime.PlayerCheck.dll"));
        foreach (var type in new[] { "ColorSlide.Gameplay.CampaignController", "ColorSlide.Gameplay.CampaignSession" })
            if (player.GetType(type).GetMethod("StartEditorLevel") != null) throw new InvalidOperationException("Editor QA API leaked into player: " + type);
        var cameraFit = player.GetType("ColorSlide.Polish.PortraitCameraFit");
        if (cameraFit.GetMethod("SetEditorZoom") != null || cameraFit.GetMethod("ResetEditorZoom") != null ||
            cameraFit.GetField("editorZoom", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic) != null ||
            player.GetType("ColorSlide.Editor.CampaignPlayModeShortcuts") != null)
            throw new InvalidOperationException("Editor camera QA controls leaked into player.");
        Console.WriteLine("Player compilation excludes level-selection and camera zoom QA entry points/state.");
        var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
        var loaded = runner.Load(args[0],new Dictionary<string,object>());
        Xml(loaded.ToXml(true),Path.Combine(args[1],"discovered.xml"));
        var filter = new OrFilter();
        foreach (var name in new[] { "GridBoardTests","ExitRulesTests","PuzzleSessionTests","CampaignTests","M4PolishTests",
            "M45BurstTests","M45FeedbackMotionTests","M46CampaignTests","M46FramingTests","M46EditorShortcutTests",
            "M46MasterTests","M46MasterFramingTests","M46CameraPolishTests","M46ZoomTests","M46UnifiedFramingTests" })
            filter.Add(new ClassNameFilter("ColorSlide.Tests.EditMode." + name));
        var tests = runner.Run(new Listener(),filter);
        Xml(tests.ToXml(true),Path.Combine(args[1],"managed.xml"));
        Console.WriteLine("Discovered=" + loaded.TestCaseCount + "; managed passed=" + tests.PassCount + "; failed=" + tests.FailCount + "; skipped=" + tests.SkipCount);
        if (tests.FailCount != 0) return 1;
        var report = new List<string>(); var levels = AdvancedCampaignLevels.Create();
        for (var i = 0; i < levels.Length; i++)
        {
            var witness = AdvancedLevelSolutions.For(i);
            report.Add(levels[i].DisplayName + ": authored witness " + witness.Length + " actions, " + AdvancedLevelSolutions.Verify(levels[i],witness) + " grid steps including full exits");
            for (var step = 0; step < witness.Length; step++) report.Add((step+1) + ". " + witness[step]);
        }
        Measure(levels[0],5000,20000,1,report);
        Measure(levels[1],5000,20000,2,report);
        if (bool.Parse(args[2])) Measure(levels[1],300000,1500000,1,report);
        var masters = MasterCampaignLevels.Create();
        for (var i = 0; i < masters.Length; i++)
        {
            var witness = MasterLevelSolutions.For(i);
            report.Add(masters[i].DisplayName + ": stored witness " + witness.Length + " actions, " + AdvancedLevelSolutions.Verify(masters[i],witness) + " grid steps");
            for (var step = 0; step < witness.Length; step++) report.Add((step+1) + ". " + witness[step]);
        }
        Measure(masters[0],50000,200000,2,report);
        Measure(masters[1],50000,200000,3,report);
        if (bool.Parse(args[3])) foreach (var master in masters) Measure(master,300000,1500000,1,report);
        File.WriteAllLines(Path.Combine(args[1],"solver-report.txt"),report);
        foreach (var line in report) Console.WriteLine(line);
        return 0;
    }
    private static void Measure(LevelData level, int expanded, int discovered, int weight, List<string> report)
    {
        var watch = Stopwatch.StartNew(); var result = PuzzleSolver.Search(level,expanded,discovered,weight);
        var shortened = result.Solved ? PuzzleSolver.SimplifyWitness(level,result.Actions) : Array.Empty<PuzzleAction>();
        if (result.Solved) AdvancedLevelSolutions.Verify(level,shortened);
        report.Add(level.DisplayName + " search: weight=" + weight + "; limits=" + expanded + " expanded/" + discovered + " discovered; " +
            "solved=" + result.Solved + "; optimal=" + result.Optimal + "; actions=" + result.Actions.Length +
            "; initial lower bound=" + result.InitialLowerBound + "; proven lower bound=" + result.ProvenLowerBound +
            "; expanded=" + result.Expanded + "; discovered=" + result.Discovered + "; milliseconds (including witness check/shortening)=" + watch.ElapsedMilliseconds +
            "; shortened actions=" + shortened.Length + "; " + result.Stop);
    }
}
