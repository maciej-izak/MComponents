﻿using System.Collections.Generic;

namespace MComponents.MGrid
{
    public interface IMGridColumn
    {
        IReadOnlyDictionary<string, object> AdditionalAttributes { get; }

        string HeaderText { get; }

        string Identifier { get; }

        bool EnableFilter { get; }

        bool ShouldRenderColumn { get; }
    }
}