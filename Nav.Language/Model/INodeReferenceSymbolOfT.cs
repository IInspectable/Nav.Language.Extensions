﻿using JetBrains.Annotations;

namespace Pharmatechnik.Nav.Language {

    public interface INodeReferenceSymbol<out T> : INodeReferenceSymbol where T : INodeSymbol {
        [CanBeNull]
        new T Declaration { get; }
    }

    public interface IInitNodeReferenceSymbol : INodeReferenceSymbol<IInitNodeSymbol> {
    }    

    public interface IChoiceNodeReferenceSymbol : INodeReferenceSymbol<IChoiceNodeSymbol> {
    } 

    public interface IGuiNodeReferenceSymbol : INodeReferenceSymbol<IGuiNodeSymbol> {
    }
}