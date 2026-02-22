namespace FSharp.Compiler.LanguageServer

type FSharpLanguageServerFeatures =
    {
        Diagnostics: bool
        SemanticHighlighting: bool
        TypeDefinition: bool
    }

    static member Default =
        {
            Diagnostics = true
            SemanticHighlighting = true
            TypeDefinition = true
        }

type FSharpLanguageServerConfig =
    {
        EnabledFeatures: FSharpLanguageServerFeatures
    }

    static member Default =
        {
            EnabledFeatures = FSharpLanguageServerFeatures.Default
        }
