using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Text.Json;
using System.Windows.Automation;
using PaceAtlas;

internal static partial class Program
{
    static int InterventionSuite(string[] args)
    {
        output=args[2];Directory.CreateDirectory(output);
        if(!string.Equals(Environment.UserName,"WDAGUtilityAccount",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Intervention UI tests require disposable Sandbox data.");
        File.WriteAllText(Path.Combine(output,"status.txt"),"RUNNING");
        try
        {
            var store=new Store();
            var start=DateTime.Today.AddDays(-14);
            store.Save(new Entry { Kind="Maßnahme",Start=start,Data=JsonSerializer.Serialize(new MeasureData {
                Name="UI test intervention",Dose="7 mg",TrackProgress=true,Goal="UI test",Phases=new() {
                    new() {Name="Initial phase",Dose="7 mg",Start=start,End=start.AddDays(7)},
                    new() {Name="Pause",Start=start.AddDays(7),Paused=true} } }) });
            for(int i=-14;i<0;i++)
            {
                store.Save(new Entry {Kind="Zustand",Start=start.AddDays(i).AddHours(12),Data=JsonSerializer.Serialize(new StateData {Overall=3,SymptomValuesVersion=2,Symptoms=new() {["Erschöpfung"]=3} })});
                store.Save(new Entry {Kind="Zustand",Start=start.AddDays(i+14).AddHours(12),Data=JsonSerializer.Serialize(new StateData {Overall=2,Pem=i%3==0?2:0,SymptomValuesVersion=2,Symptoms=new() {["Erschöpfung"]=2} })});
                store.Save(new Entry {Kind="Aktivität",Start=start.AddDays(i).AddHours(9),End=start.AddDays(i).AddHours(10)});
                store.Save(new Entry {Kind="Aktivität",Start=start.AddDays(i+14).AddHours(9),End=start.AddDays(i+14).AddHours(10)});
            }
            app=Process.Start(new ProcessStartInfo(args[1]){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(args[1])!})!;
            var main=WaitHandleWindow("Pace Atlas",45000,false);
            Wait(()=>IsWindowVisible(main),10000,"Main window not visible");SetForegroundWindow(main);
            var root=AutomationElement.FromHandle(main);
            AutomationElement Find(string name, ControlType type) {
                AutomationElement? found=null;
                Wait(()=>(found=root.FindFirst(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.NameProperty,name),new PropertyCondition(AutomationElement.ControlTypeProperty,type))))!=null,10000,"Missing UI: "+name);
                return found!;
            }
            void Select(string name) { ((SelectionItemPattern)Find(name,ControlType.TabItem).GetCurrentPattern(SelectionItemPattern.Pattern)).Select();Thread.Sleep(300); }
            void Invoke(string name) { ((InvokePattern)Find(name,ControlType.Button).GetCurrentPattern(InvokePattern.Pattern)).Invoke();Thread.Sleep(300); }
            Select("Maßnahmen");Invoke("Phase hinzufügen");
            AutomationElement? field=null;
            Wait(()=>(field=root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"InterventionPhaseName")))!=null,5000,"Phase editor missing");
            ((ValuePattern)field!.GetCurrentPattern(ValuePattern.Pattern)).SetValue("UI new phase");
            Invoke("Übernehmen");Invoke("Maßnahme speichern");
            Wait(()=>store.All().Where(e=>e.Kind=="Maßnahme").Any(e=>InterventionAnalysis.Read<MeasureData>(e)?.Phases.Any(p=>p.Name=="UI new phase")==true),5000,"Phase not persisted");
            Log("PASS phase editor and persisted existing measure JSON");
            Select("Auswertung");Select("Interventionsanalyse");
            // The newly created measure is selected first; choose the seeded one in the existing combo.
            var combo=root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"InterventionMeasure"));
            Require(combo!=null,"Intervention selector missing");
            ((ExpandCollapsePattern)combo!.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Expand();Thread.Sleep(200);
            var option=AutomationElement.RootElement.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ListItem))
                .Cast<AutomationElement>().FirstOrDefault(e=>e.Current.Name.Contains("UI test intervention"));
            Require(option!=null,"Seeded measure option missing");
            ((SelectionItemPattern)option!.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();Thread.Sleep(200);
            Wait(()=>root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Text))
                .Cast<AutomationElement>().Any(e=>e.Current.Name.Contains("Differenz:") && e.Current.Name.Contains("-1")),5000,"Expected calculated difference not rendered");
            Log("PASS selected measure, default periods, symptoms/activity/PEM results");Capture("intervention-analysis");
            var phaseCombo=root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"InterventionBaselinePhase"));
            ((ExpandCollapsePattern)phaseCombo!.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Expand();Thread.Sleep(200);
            var phaseOption=AutomationElement.RootElement.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ListItem))
                .Cast<AutomationElement>().FirstOrDefault(e=>e.Current.Name.Contains("Initial phase"));
            Require(phaseOption!=null,"Saved phases not offered");
            ((SelectionItemPattern)phaseOption!.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();Thread.Sleep(200);
            var followCombo=root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"InterventionFollowPhase"));
            ((ExpandCollapsePattern)followCombo!.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Expand();Thread.Sleep(200);
            var followOption=AutomationElement.RootElement.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ListItem))
                .Cast<AutomationElement>().FirstOrDefault(e=>e.Current.Name.Contains("Pause"));
            Require(followOption!=null,"Follow-up phase missing");
            ((SelectionItemPattern)followOption!.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();Thread.Sleep(200);
            Wait(()=>root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Text))
                .Cast<AutomationElement>().Any(e=>e.Current.Name.Contains("Vergleich:") && e.Current.Name.Contains("7/8 Tage")),5000,"Independent phase comparison not rendered");
            Log("PASS selecting independent baseline and follow-up phases and recalculating");
            Require(root.FindFirst(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button),
                new PropertyCondition(AutomationElement.NameProperty,"Zeiträume vergleichen")))==null,"Manual compare button should be removed");
            app.Kill(true); app.WaitForExit(); app.Dispose();
            app=Process.Start(new ProcessStartInfo(args[1]){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(args[1])!})!;
            main=WaitHandleWindow("Pace Atlas",45000,false); root=AutomationElement.FromHandle(main);
            Select("Auswertung");
            Wait(()=>root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Text))
                .Cast<AutomationElement>().Any(e=>e.Current.Name.Contains("Vergleich:") && e.Current.Name.Contains("7/8 Tage")),10000,"Saved phase selection not evaluated automatically after restart");
            Log("PASS automatic evaluation after process restart, without compare button");
            File.WriteAllText(Path.Combine(output,"status.txt"),"PASS interventions");return 0;
        }
        catch(Exception ex) {Log("FAIL: "+ex);Capture("intervention-failure");File.WriteAllText(Path.Combine(output,"status.txt"),"FAIL: "+ex.Message);return 1;}
        finally { if(app is {HasExited:false}) app.Kill(true); }
    }
}
