﻿#region Using Directives

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Pharmatechnik.Nav.Language.Extension.Common;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Outlining; 

[Export(typeof(ITaggerProvider))]
[TagType(typeof(IOutliningRegionTag))]
[ContentType(NavLanguageContentDefinitions.ContentType)]
sealed class OutliningTaggerProvider: ITaggerProvider {

    readonly CodeContentControlProvider _codeContentControlProvider;

    [ImportingConstructor]
    public OutliningTaggerProvider(CodeContentControlProvider codeContentControlProvider) {
        _codeContentControlProvider = codeContentControlProvider;
    }
       
    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag {
        return new OutliningTagger(buffer, _codeContentControlProvider) as ITagger<T>;
    }
}