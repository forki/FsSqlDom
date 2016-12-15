﻿namespace FsSqlDom

open System
open System.Collections.Generic
open System.IO
open Microsoft.SqlServer.TransactSql

type ParseFragmentResult =
  | Success of TSqlFragment
  | Failure of IList<ScriptDom.ParseError>

module private Internal =
  let defaultWriterOpts : SqlWriterOptions = { capitalizedKeywords = false}

type Util =
  static member parse(tr:TextReader, initialQuotedIdentifiers:bool) =
    let parser = ScriptDom.TSql130Parser(initialQuotedIdentifiers)
    let mutable errs : IList<_> = Unchecked.defaultof<IList<_>>
    let res = parser.Parse(tr, &errs)

    if errs.Count = 0 then
      let converted = FsSqlDom.TSqlFragment.FromTs(res)
      ParseFragmentResult.Success(converted)
    else
      ParseFragmentResult.Failure(errs)
    
  static member parse(s:string, ?initialQuotedIdentifiers:bool) : ParseFragmentResult =
    let initialQuotedIdentifiers = defaultArg initialQuotedIdentifiers false
    use tr = new StringReader(s) :> TextReader
    Util.parse(tr, initialQuotedIdentifiers)

  static member parseExpr(s:string, ?initialQuotedIdentifiers:bool) : Choice<ScalarExpression, IList<ScriptDom.ParseError>> =
    let initialQuotedIdentifiers = defaultArg initialQuotedIdentifiers false
    let parser = ScriptDom.TSql130Parser(initialQuotedIdentifiers)
    let mutable errs : IList<_> = Unchecked.defaultof<IList<_>>
    use tr = new StringReader(s) :> TextReader
    let res = parser.ParseExpression(tr, &errs)

    if errs.Count = 0 then
      let converted = ScalarExpression.FromTs(res)
      Choice1Of2(converted)
    else
      Choice2Of2(errs)

  static member getQueryExpr(frag: TSqlFragment) : QueryExpression option =
    match frag with
    | TSqlFragment.TSqlScript(script::_) ->
      match script with
      | TSqlBatch.TSqlBatch(statement::_) ->
        match statement with
        | TSqlStatement.StatementWithCtesAndXmlNamespaces(StatementWithCtesAndXmlNamespaces.SelectStatement(SelectStatement.Base(_,_,_,qexpr,_))) ->
          qexpr
        | _ -> None
      | _ -> None
    | _ -> None

  static member render(qexpr:QueryExpression) : string =
    let w = SqlWriter(Internal.defaultWriterOpts)
    w.write(qexpr)
    w.ToString()