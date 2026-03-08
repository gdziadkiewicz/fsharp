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

    interface IRequestHandler<TextDocumentPositionParams, Nullable<SumType<Location, Location[]>>, FSharpRequestContext> with
        [<LanguageServerEndpoint(Methods.TextDocumentTypeDefinitionName, LanguageServerConstants.DefaultLanguageName)>]
        member _.HandleRequestAsync
            (request: TextDocumentPositionParams, context: FSharpRequestContext, cancellationToken: CancellationToken)
            =
            cancellableTask {
                let config = context.LspServices.GetRequiredService<FSharpLanguageServerConfig>()

                if not config.EnabledFeatures.TypeDefinition then
                    return Nullable()
                else
                    let file = request.TextDocument.Uri
                    let line = int request.Position.Line + 1
                    let column = int request.Position.Character

                    let! typeDefinitionRange = context.Workspace.Query.GetTypeDefinitionForFile(file, line, column)

                    match typeDefinitionRange with
                    | None ->
                        return Nullable()
                    | Some typeDefinitionRange ->
                        let location = Location()
                        location.Uri <- Uri(typeDefinitionRange.FileName)
                        location.Range <- typeDefinitionRange.ToLspRange()

                        return Nullable(SumType<Location, Location[]>(location))
            }
            |> CancellableTask.start cancellationToken
