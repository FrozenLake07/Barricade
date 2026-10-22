using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.IO;

public static class CampaignPlaytestStorage
{
    public static void WriteAtomic(string path,string text)
    {
        string temporary=path+".tmp";
        File.WriteAllText(temporary,text,new UTF8Encoding(true));
        if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
    }
}

[Serializable] public sealed class CampaignAttemptMetrics
{
    public int attempt,chapter,startHP,endHP,minHP,maxHP,firstFullBeat=-1,judgements,perfect,ordinary,missed;
    public int damage,healed,overflowHealing,shots,emptyShots,reloads,perfectReloads,rescues,rests,shellInterceptions;
    public int escortSuccesses,escortFailures,completedBeats,carriedEscortSuccesses,carriedEscortFailures;
    public int ammoCapacity=3;
    public float reloadMultiplier=1;
    public int preparationJudgements,preparationPerfect,defenseJudgements,defensePerfect;
    public string title,outcome="in_progress",decision="Unchosen",waveVariant="",lastPhase="",origin;
    public double activeSeconds,pausedSeconds,transitionSeconds,resultsSeconds,preparationSeconds,defenseSeconds,fullHealthPreparationSeconds;
    public double firstFullActiveSeconds=-1;
}

// No engine APIs: the same aggregation code is exercised by offline regression tests.
public sealed class CampaignMetrics
{
    public readonly List<CampaignAttemptMetrics> attempts=new List<CampaignAttemptMetrics>();
    public CampaignAttemptMetrics Current {get;private set;}
    public void Begin(int chapter,string title,int health,int limit,string origin)
    {
        End("interrupted");
        Current=new CampaignAttemptMetrics{attempt=attempts.Count+1,chapter=chapter,title=title,startHP=health,endHP=health,minHP=health,maxHP=health,origin=origin};
        attempts.Add(Current);
        if(health>=limit&&limit>0){Current.firstFullBeat=0;Current.firstFullActiveSeconds=0;}
    }
    public void Tick(double delta,bool paused,string phase,int health,int limit,int beat)
    {
        var row=Current;if(row==null||double.IsNaN(delta)||double.IsInfinity(delta)||delta<0)return;
        row.lastPhase=phase;
        ObserveHealth(health,limit,beat);
        if(paused){row.pausedSeconds+=delta;return;}
        if(phase=="Transition"||phase=="EndCard"){row.transitionSeconds+=delta;return;}
        if(phase=="Results"){row.resultsSeconds+=delta;return;}
        if(phase!="Preparation"&&phase!="Defense"&&phase!="Choice"&&phase!="Cinematic"&&phase!="Epilogue"&&phase!="LastChance")return;
        row.activeSeconds+=delta;
        if(phase=="Preparation"){
            row.preparationSeconds+=delta;
            if(health>=limit&&limit>0)row.fullHealthPreparationSeconds+=delta;
        }
        if(phase=="Defense")row.defenseSeconds+=delta;
    }
    public void ObserveHealth(int health,int limit,int beat)
    {
        var row=Current;if(row==null)return;
        row.endHP=health;row.minHP=Math.Min(row.minHP,health);row.maxHP=Math.Max(row.maxHP,health);
        if(row.firstFullBeat<0&&limit>0&&health>=limit){row.firstFullBeat=beat;row.firstFullActiveSeconds=row.activeSeconds;}
    }
    public void HealthChanged(int before,int after,int requested,int limit,int beat)
    {
        var row=Current;if(row==null)return;
        row.damage+=Math.Max(0,before-after);row.healed+=Math.Max(0,after-before);
        row.overflowHealing+=Math.Max(0,requested-Math.Max(0,after-before));ObserveHealth(after,limit,beat);
    }
    public void Judge(bool perfect,bool missed,string phase="")
    {
        var row=Current;if(row==null)return;
        row.judgements++;if(perfect)row.perfect++;else if(missed)row.missed++;else row.ordinary++;
        if(phase=="Preparation"){row.preparationJudgements++;if(perfect)row.preparationPerfect++;}
        if(phase=="Defense"){row.defenseJudgements++;if(perfect)row.defensePerfect++;}
    }
    public void Event(string kind)
    {
        var row=Current;if(row==null)return;
        switch(kind){case "shot":row.shots++;break;case "empty_shot":row.emptyShots++;break;
            case "reload":row.reloads++;break;case "perfect_reload":row.reloads++;row.perfectReloads++;break;
            case "rescue":row.rescues++;break;case "rest":row.rests++;break;case "shell_intercept":row.shellInterceptions++;break;}
    }
    public void End(string outcome){if(Current==null)return;Current.outcome=outcome;Current=null;}
    public static string CsvCell(string value)
    {
        value=value??"";
        // Keep names/labels from becoming formulas when opened in spreadsheet software.
        if(value.Length>0&&"=+-@\t\r".IndexOf(value[0])>=0)value="'"+value;
        return "\""+value.Replace("\"","\"\"")+"\"";
    }
    static string N(double value)=>value.ToString("0.###",CultureInfo.InvariantCulture);
    public string Csv(bool nonDesign=false)
    {
        var b=new StringBuilder("attempt,chapter,title,origin,outcome,active_seconds,paused_seconds,transition_seconds,preparation_seconds,defense_seconds,start_hp,end_hp,min_hp,first_full_beat,first_full_active_seconds,full_hp_preparation_seconds,damage,healed,overflow_healing,judgements,perfect,ordinary,missed,perfect_rate,shots,empty_shots,reloads,perfect_reloads,rescues,rests,shell_interceptions,escort_successes,escort_failures,decision,wave_variant,completed_beats,preparation_judgements,preparation_perfect,defense_judgements,defense_perfect,carried_escort_successes,carried_escort_failures,reload_multiplier,ammo_capacity,non_design\n");
        foreach(var r in attempts){
            b.Append(r.attempt).Append(',').Append(r.chapter).Append(',').Append(CsvCell(r.title)).Append(',').Append(CsvCell(r.origin)).Append(',').Append(CsvCell(r.outcome)).Append(',');
            b.Append(N(r.activeSeconds)).Append(',').Append(N(r.pausedSeconds)).Append(',').Append(N(r.transitionSeconds)).Append(',').Append(N(r.preparationSeconds)).Append(',').Append(N(r.defenseSeconds)).Append(',');
            b.Append(r.startHP).Append(',').Append(r.endHP).Append(',').Append(r.minHP).Append(',').Append(r.firstFullBeat).Append(',').Append(N(r.firstFullActiveSeconds)).Append(',').Append(N(r.fullHealthPreparationSeconds)).Append(',');
            b.Append(r.damage).Append(',').Append(r.healed).Append(',').Append(r.overflowHealing).Append(',').Append(r.judgements).Append(',').Append(r.perfect).Append(',').Append(r.ordinary).Append(',').Append(r.missed).Append(',').Append(N(r.judgements>0?(double)r.perfect/r.judgements:0)).Append(',');
            b.Append(r.shots).Append(',').Append(r.emptyShots).Append(',').Append(r.reloads).Append(',').Append(r.perfectReloads).Append(',').Append(r.rescues).Append(',').Append(r.rests).Append(',').Append(r.shellInterceptions).Append(',').Append(r.escortSuccesses).Append(',').Append(r.escortFailures).Append(',').Append(CsvCell(r.decision)).Append(',').Append(CsvCell(r.waveVariant)).Append(',').Append(r.completedBeats).Append(',').Append(r.preparationJudgements).Append(',').Append(r.preparationPerfect).Append(',').Append(r.defenseJudgements).Append(',').Append(r.defensePerfect).Append(',').Append(r.carriedEscortSuccesses).Append(',').Append(r.carriedEscortFailures).Append(',').Append(N(r.reloadMultiplier)).Append(',').Append(r.ammoCapacity).Append(',').Append(nonDesign?"非设计值":"设计值").Append('\n');
        }
        return b.ToString();
    }
}
