namespace FSharp.Compiler.LanguageServer.Handlers

open Microsoft.CommonLanguageServerProtocol.Framework
open Microsoft.VisualStudio.LanguageServer.Protocol
open Microsoft.VisualStudio.FSharp.Editor.CancellableTasks
open FSharp.Compiler.LanguageServer.Common
open FSharp.Compiler.LanguageServer

open System
open System.Threading
open Microsoft.Extensions.DependencyInjection

#nowarn "57"

type GoToTypeDefinitionHandler() =
    interface IMethodHandler with
        member _.MutatesSolutionState = false

    interface IRequestHandler<TypeDefinitionParams, Location[], FSharpRequestContext> with
        [<LanguageServerEndpoint("textDocument/typeDefinition", LanguageServerConstants.DefaultLanguageName)>]
        member _.HandleRequestAsync(request: TypeDefinitionParams, context: FSharpRequestContext, cancellationToken: CancellationToken) =
            cancellableTask {
                let config = context.LspServices.GetRequiredService<FSharpLanguageServerConfig>()

                if not config.EnabledFeatures.TypeDefinition then
                    return [||]

                let file = request.TextDocument.Uri
                let line = request.Position.Line + 1
                let column = request.Position.Character

                let! typeDefinitionRange = context.Workspace.Query.GetTypeDefinitionForFile(file, line, column)

                match typeDefinitionRange with
                | None ->
                    return [||]
                | Some typeDefinitionRange ->
                    let location =
                        Location(Uri(typeDefinitionRange.FileName), typeDefinitionRange.ToLspRange())

                    return [| location |]
            }
            |> CancellableTask.start cancellationToken
