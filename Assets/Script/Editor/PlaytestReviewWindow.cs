#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class PlaytestReviewWindow : EditorWindow
{
    [Serializable] public sealed class Run {public bool nonDesign;public string runId,label,configurationId,status;public CampaignAttemptMetrics[] attempts;}
    readonly List<Run> runs=new List<Run>();ScrollView content;PopupField<string> baseline,candidate;Label status;
    [MenuItem("街垒/测试/查看试玩统计与对比")]
    public static void Open(){var w=GetWindow<PlaytestReviewWindow>("试玩统计与对比");w.minSize=new Vector2(780,500);w.Show();}
    public void CreateGUI()
    {
        var root=rootVisualElement;root.Clear();root.style.paddingLeft=root.style.paddingRight=12;root.style.paddingTop=root.style.paddingBottom=10;
        root.Add(new HelpBox("每次新游戏或读档创建独立记录。对比只统计完整的completed/failed/defeated尝试，排除读档片段、正在进行和中途退出。均值是描述性统计，请用多次试玩判断。",HelpBoxMessageType.Info));
        runs.Clear();int errors=0;
        if(Directory.Exists(CampaignTelemetry.RootPath))foreach(var folder in Directory.GetDirectories(CampaignTelemetry.RootPath).OrderByDescending(p=>p)){
            try{string file=Path.Combine(folder,"run.json");if(!File.Exists(file))continue;var run=JsonUtility.FromJson<Run>(File.ReadAllText(file));if(run?.attempts!=null)runs.Add(run);}catch{errors++;}
        }
        var buttons=new VisualElement();buttons.style.flexDirection=FlexDirection.Row;root.Add(buttons);
        buttons.Add(new Button(CreateGUI){text="刷新记录"});buttons.Add(new Button(ChapterStructureWindow.OpenData){text="打开数据目录"});buttons.Add(new Button(Export){text="导出当前对比CSV"});
        if(runs.Count==0){root.Add(new HelpBox("尚无试玩记录。从Title开始新游戏并游玩后再刷新。",HelpBoxMessageType.Info));return;}
        var choices=runs.Select(r=>r.runId+" · "+r.label+(r.nonDesign?" · 非设计值":"")).ToList();
        baseline=new PopupField<string>("基准记录",choices,Math.Min(1,runs.Count-1));candidate=new PopupField<string>("调整后记录",choices,0);
        root.Add(baseline);root.Add(candidate);baseline.RegisterValueChangedCallback(_=>Render());candidate.RegisterValueChangedCallback(_=>Render());
        status=new Label(errors>0?"跳过无法读取的记录："+errors:"");root.Add(status);
        content=new ScrollView();content.style.flexGrow=1;root.Add(content);Render();
    }
    static IEnumerable<CampaignAttemptMetrics> Complete(Run run,int chapter)=>run.nonDesign?Enumerable.Empty<CampaignAttemptMetrics>():run.attempts.Where(r=>r.chapter==chapter&&r.origin!="loaded_partial"&&(r.outcome=="completed"||r.outcome=="failed"||r.outcome=="defeated"));
    static string N(double value)=>value.ToString("0.##",CultureInfo.InvariantCulture);
    static string Summary(CampaignAttemptMetrics[] rows)
    {
        if(rows.Length==0)return "暂无完整尝试";
        int completed=rows.Count(r=>r.outcome=="completed"),judged=rows.Sum(r=>r.judgements);double prep=rows.Sum(r=>r.preparationSeconds);
        return $"尝试 {rows.Length} / 完成 {completed}；Perfect {N(judged>0?100d*rows.Sum(r=>r.perfect)/judged:0)}%；平均有效游玩 {N(rows.Average(r=>r.activeSeconds))} 秒\n平均入章/出章耐久 {N(rows.Average(r=>r.startHP))}/{N(rows.Average(r=>r.endHP))}；平均受伤 {N(rows.Average(r=>r.damage))}；修补满血时间占比 {N(prep>0?100d*rows.Sum(r=>r.fullHealthPreparationSeconds)/prep:0)}%\n开火 {rows.Sum(r=>r.shots)}；空击 {rows.Sum(r=>r.emptyShots)}；换弹 {rows.Sum(r=>r.reloads)}；抢救 {rows.Sum(r=>r.rescues)}";
    }
    void Render()
    {
        content.Clear();var a=runs[baseline.index];var b=runs[candidate.index];
        if(baseline.index==candidate.index)content.Add(new HelpBox("目前选择了同一记录；至少两次试玩后可以比较调整前后。",HelpBoxMessageType.Info));
        if(a.nonDesign||b.nonDesign)content.Add(new HelpBox("所选记录含控制台调试覆盖（非设计值），已从正式难度统计与导出中排除。",HelpBoxMessageType.Warning));
        var identity=new Label("基准配置："+a.configurationId+"\n调整配置："+b.configurationId);identity.style.whiteSpace=WhiteSpace.Normal;content.Add(identity);
        for(int n=1;n<=10;n++){
            var left=Complete(a,n).ToArray();var right=Complete(b,n).ToArray();if(left.Length+right.Length==0)continue;
            string title=right.FirstOrDefault()?.title??left.FirstOrDefault()?.title;
            var fold=new Foldout{text="第"+n+"章 · "+title,value=true};content.Add(fold);
            var l=new Label("基准："+Summary(left)+"\n调整后："+Summary(right));l.style.whiteSpace=WhiteSpace.Normal;fold.Add(l);
        }
    }
    void Export()
    {
        if(runs.Count==0||candidate==null)return;
        var text=new StringBuilder("run_id,label,configuration_id,chapter,attempts,completed,perfect_rate,mean_active_seconds,mean_start_hp,mean_end_hp,mean_damage,full_hp_preparation_ratio\n");
        foreach(var run in new[]{runs[baseline.index],runs[candidate.index]})for(int n=1;n<=10;n++){
            var rows=Complete(run,n).ToArray();if(rows.Length==0)continue;
            int judged=rows.Sum(r=>r.judgements);double prep=rows.Sum(r=>r.preparationSeconds);
            text.Append(CampaignMetrics.CsvCell(run.runId)).Append(',').Append(CampaignMetrics.CsvCell(run.label)).Append(',').Append(CampaignMetrics.CsvCell(run.configurationId)).Append(',').Append(n).Append(',').Append(rows.Length).Append(',').Append(rows.Count(r=>r.outcome=="completed")).Append(',');
            text.Append(N(judged>0?(double)rows.Sum(r=>r.perfect)/judged:0)).Append(',').Append(N(rows.Average(r=>r.activeSeconds))).Append(',').Append(N(rows.Average(r=>r.startHP))).Append(',').Append(N(rows.Average(r=>r.endHP))).Append(',').Append(N(rows.Average(r=>r.damage))).Append(',').Append(N(prep>0?rows.Sum(r=>r.fullHealthPreparationSeconds)/prep:0)).Append('\n');
        }
        string path=EditorUtility.SaveFilePanel("导出试玩对比",CampaignTelemetry.RootPath,"playtest-comparison","csv");
        if(!string.IsNullOrEmpty(path))try{File.WriteAllText(path,text.ToString(),new UTF8Encoding(true));status.text="对比已导出。";}catch(Exception e){status.text="导出失败："+e.Message;}
    }
}
#endif
