#region Using Directives

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

#endregion

namespace Pharmatechnik.Nav.Language.BuildTasks {

    public class Nav: ToolTask {

        public bool Force  { get; set; }
        public bool Strict { get; set; }

        public ITaskItem[] Sources { get; set; }

        public bool GenerateToClasses   { get; set; }
        public bool GenerateWflClasses  { get; set; }
        public bool GenerateIwflClasses { get; set; }

        IEnumerable<CodeGenerationOptions> GetCodeGenerationOptions() {

            var options = new (Func<bool> IsOn, CodeGenerationOptions EnumValue)[] {
                (() => GenerateToClasses,   CodeGenerationOptions.ToClasses),
                (() => GenerateWflClasses,  CodeGenerationOptions.WflClasses),
                (() => GenerateIwflClasses, CodeGenerationOptions.IwflClasses),
            };

            if (options.All(o => !o.IsOn())) {
                yield return CodeGenerationOptions.None;
                yield break;
            }

            foreach (var option in options.Where(o => o.IsOn())) {
                yield return option.EnumValue;
            }
           
        }

        public bool UseSyntaxCache  { get; set; }
        public bool FullPaths       { get; set; }
        public bool NullableContext { get; set; }

        public ITaskItem   ProjectRootDirectory { get; set; }
        public ITaskItem   IwflRootDirectory    { get; set; }
        public ITaskItem   WflRootDirectory     { get; set; }

        public string      ManifestFile         { get; set; }
        
        protected override string GenerateFullPathToTool() {
            // ReSharper disable once AssignNullToNotNullAttribute
            return Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location), ToolName);
        }

        protected override string ToolName                 => "nav.exe";
        protected override Encoding ResponseFileEncoding   => Encoding.UTF8;
        protected override Encoding StandardOutputEncoding => Encoding.UTF8;

        protected override string GenerateResponseFileCommands() {

            var clb = new CommandLineBuilder();

            clb.AppendSwitchIfPresent(Force,          "/f");
            clb.AppendSwitchIfPresent(Strict,         "/t");
            clb.AppendSwitchIfPresent(UseSyntaxCache, "/c");
            clb.AppendSwitchIfPresent(FullPaths,      "/fullpaths");
            clb.AppendSwitchIfPresent(NullableContext, "/n");
            clb.AppendSwitch("/v");
            clb.AppendSwitchIfNotNull("/g:", GetGetCodeGenerationArg());
            clb.AppendSwitchIfNotNull("/r:", ProjectRootDirectory);
            clb.AppendSwitchIfNotNull("/w:", WflRootDirectory);
            clb.AppendSwitchIfNotNull("/i:", IwflRootDirectory);
            if (!string.IsNullOrEmpty(ManifestFile)) {
                clb.AppendSwitchIfNotNull("/m:", ManifestFile);
            }
            clb.AppendSwitchIfNotNull("/s:", Sources, " /s:");

            return clb.ToString();


             string GetGetCodeGenerationArg() {
                 var options = GetCodeGenerationOptions().ToList();
                 if (!options.Any()) {
                     return CodeGenerationOptions.None.ToString();
                 }

                 return string.Join(",", options);
             }
        }

        protected override bool ValidateParameters() {            
            return true;
        }
        
        protected override bool SkipTaskExecution() {
            return (Sources?.Length ?? 0) == 0;
        }
        
        protected override void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance) {

            const string verbosePrefix = "Verbose:";

            if (singleLine.StartsWith(verbosePrefix)) {
                messageImportance = MessageImportance.Low;
                singleLine = singleLine.Substring(verbosePrefix.Length);
            }

            base.LogEventsFromTextOutput(singleLine, messageImportance);            
        }

        protected override MessageImportance StandardOutputLoggingImportance => MessageImportance.High;
    }
}