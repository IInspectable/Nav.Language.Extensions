﻿#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes {
    public class RemoveUnusedNodesCodeFix : CodeFix {

        internal RemoveUnusedNodesCodeFix(ITaskDefinitionSymbol taskDefinitionSymbol, CodeFixContext context)
            : base(context) {
            TaskDefinition = taskDefinitionSymbol ?? throw new ArgumentNullException(nameof(taskDefinitionSymbol));
        }

        public override string Name              => "Remove Unused Nodes";
        public override CodeFixImpact Impact     => CodeFixImpact.None;
        public override TextExtent? ApplicableTo => null;
        public override CodeFixPrio Prio         => CodeFixPrio.Medium;
        public ITaskDefinitionSymbol TaskDefinition { get; }

        internal bool CanApplyFix() {
            return GetCandidates().Any();
        }

        IEnumerable<INodeSymbol> GetCandidates() {
            return TaskDefinition.NodeDeclarations.Where(n => n.References.Count == 0);
        }

        public IList<TextChange> GetTextChanges() {

            var textChanges = new List<TextChange?>();
            foreach (var textChange in GetCandidates().SelectMany(c => GetRemoveSyntaxNodeChanges(c.Syntax))) {
                textChanges.Add(textChange);
            }
            return textChanges.OfType<TextChange>().ToList();
        }
    }
}