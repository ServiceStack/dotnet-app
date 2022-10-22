using System;
using System.Collections.Generic;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;

namespace Web.Dtos;

public class GitHubActionDtos
{
    
}

public class Workflow
{
    [YamlMember]
    public Dictionary<string,Job> Jobs { get; set; }
}

public class Job
{
    [YamlMember]
    public List<Step> Steps { get; set; }
}

public class Step
{
    public string Name { get; set;}
    public string Uses { get; set; }
    [YamlMember]
    public Dictionary<string,string> Env { get; set; }
    [YamlMember]
    public Dictionary<string,string> With { get; set; }
}

public class ReplacementStep
{
    public string StepKey { get; set;}
    public string JobKey { get; set; }
    public Step Step { get; set; }
}

public class ActionWithEmitter : ChainedEventEmitter
{
    public ActionWithEmitter(IEventEmitter nextEmitter) : base(nextEmitter)
    {
        
    }

    public override void Emit(ScalarEventInfo eventInfo, IEmitter emitter)
    {
        if(eventInfo.Source?.Value != null && 
           eventInfo.Source.Type == typeof(string) && 
           eventInfo.Source.Value.ToString()!.Contains("\n"))
            eventInfo.Style = ScalarStyle.Literal;
        base.Emit(eventInfo, emitter);
    }
}

