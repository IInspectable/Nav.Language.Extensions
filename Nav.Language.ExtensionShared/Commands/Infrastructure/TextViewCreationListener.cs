﻿#region Using Directives

using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.TextManager.Interop;
using Pharmatechnik.Nav.Language.Extension.Common;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Commands; 

[Export(typeof(IVsTextViewCreationListener))]
[ContentType(NavLanguageContentDefinitions.ContentType)]
[TextViewRole(PredefinedTextViewRoles.Interactive)]
sealed class TextViewCreationListener : IVsTextViewCreationListener {

    readonly ICommandHandlerServiceProvider  _commandHandlerServiceProvider;
    readonly IVsEditorAdaptersFactoryService _editorAdaptersFactory;

    [ImportingConstructor]
    public TextViewCreationListener(
        IVsEditorAdaptersFactoryService editorAdaptersFactory,
        ICommandHandlerServiceProvider commandHandlerServiceProvider) {

        _editorAdaptersFactory         = editorAdaptersFactory;
        _commandHandlerServiceProvider = commandHandlerServiceProvider;            
    }

    public void VsTextViewCreated(IVsTextView textView) {

        var wpfTextView = _editorAdaptersFactory.GetWpfTextView(textView);

        wpfTextView.GetOrCreateAutoClosingProperty(wpfTextView,
                                                   tv => new CommandTarget(tv, _commandHandlerServiceProvider, _editorAdaptersFactory)

        );
    }
}