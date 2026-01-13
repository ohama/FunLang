module FunLang.CommentCollector

open FunLang.Ast
open FunLang.GeneratedParser

// =============================================================================
// Comment Types
// =============================================================================

/// 주석 정보
type Comment = {
    Text: string           // 주석 내용 (// 제외)
    Pos: Position          // 시작 위치 (// 위치)
    Kind: CommentKind
}

and CommentKind =
    | LineComment          // // ...
    // | BlockComment      // 추후 확장: /* ... */

/// 토큰과 연관된 주석
type TokenWithComment = {
    Token: token
    Pos: Position
    PrecedingComment: Comment option  // 이 토큰 직전의 주석
}

// =============================================================================
// Comment Collection
// =============================================================================

/// 토큰 스트림에서 COMMENT 토큰 분리 및 연결
/// 각 COMMENT는 다음 non-COMMENT 토큰에 연결됨
let collectAndAttachComments (tokens: (token * Position) list) : TokenWithComment list * Comment list =
    let rec loop (pending: Comment option) (tokens: (token * Position) list)
                 (acc: TokenWithComment list) (allComments: Comment list) =
        match tokens with
        | [] ->
            // 남은 pending comment가 있으면 마지막 토큰에 연결
            (List.rev acc, List.rev allComments)

        | (COMMENT text, pos) :: rest ->
            // COMMENT 토큰 발견 - pending으로 저장
            let comment = { Text = text; Pos = pos; Kind = LineComment }
            loop (Some comment) rest acc (comment :: allComments)

        | (tok, pos) :: rest ->
            // 일반 토큰 - pending comment 연결
            let tokenWithComment = {
                Token = tok
                Pos = pos
                PrecedingComment = pending
            }
            loop None rest (tokenWithComment :: acc) allComments

    loop None tokens [] []

/// COMMENT 토큰을 제거하고 나머지 토큰만 반환 (파싱용)
let filterComments (tokens: (token * Position) list) : (token * Position) list * Comment list =
    let rec loop (tokens: (token * Position) list)
                 (acc: (token * Position) list) (comments: Comment list) =
        match tokens with
        | [] -> (List.rev acc, List.rev comments)
        | (COMMENT text, pos) :: rest ->
            let comment = { Text = text; Pos = pos; Kind = LineComment }
            loop rest acc (comment :: comments)
        | tok :: rest ->
            loop rest (tok :: acc) comments

    loop tokens [] []

// =============================================================================
// Comment Position Classification
// =============================================================================

/// 주석이 trailing인지 확인 (같은 줄에 코드가 있음)
let isTrailingComment (comment: Comment) (precedingTokenPos: Position option) : bool =
    match precedingTokenPos with
    | Some tokenPos -> comment.Pos.Line = tokenPos.Line
    | None -> false

/// 주석이 leading인지 확인 (다음 줄에 코드가 있음)
let isLeadingComment (comment: Comment) (followingTokenPos: Position option) : bool =
    match followingTokenPos with
    | Some tokenPos -> comment.Pos.Line < tokenPos.Line
    | None -> false

// =============================================================================
// Comment Map for Formatting
// =============================================================================

type CommentAttachment =
    | Leading   // 다음 노드에 속함 (노드 위에 출력)
    | Trailing  // 이전 노드에 속함 (노드 옆에 출력)

type AttachedComment = {
    Comment: Comment
    Attachment: CommentAttachment
}

/// 주석을 줄 번호 기반으로 분류
/// - Trailing: 주석과 같은 줄에 토큰이 있으면 trailing
/// - Leading: 그 외의 경우 leading
let classifyComments (comments: Comment list) (tokens: (token * Position) list) : AttachedComment list =
    // 각 줄에 있는 토큰 위치 맵
    let tokenLineMap =
        tokens
        |> List.filter (fun (tok, _) ->
            match tok with
            | NEWLINE | INDENT | DEDENT | EOF -> false
            | _ -> true)
        |> List.map (fun (_, pos) -> (pos.Line, pos))
        |> Map.ofList

    comments
    |> List.map (fun comment ->
        // 같은 줄에 토큰이 있는지 확인
        let hasTokenOnSameLine = Map.containsKey comment.Pos.Line tokenLineMap
        let attachment =
            if hasTokenOnSameLine then Trailing
            else Leading
        { Comment = comment; Attachment = attachment }
    )

// =============================================================================
// Comment Lookup Helpers
// =============================================================================

/// 특정 줄의 leading 주석들 가져오기 (해당 줄 바로 위의 주석들)
let getLeadingComments (attachedComments: AttachedComment list) (targetLine: int) : Comment list =
    attachedComments
    |> List.filter (fun ac ->
        ac.Attachment = Leading && ac.Comment.Pos.Line < targetLine)
    |> List.filter (fun ac ->
        // targetLine 바로 위의 연속된 주석만
        ac.Comment.Pos.Line >= targetLine - 10)  // 적당한 범위 내
    |> List.map (fun ac -> ac.Comment)
    |> List.sortBy (fun c -> c.Pos.Line)

/// 특정 줄의 trailing 주석 가져오기 (해당 줄 끝의 주석)
let getTrailingComment (attachedComments: AttachedComment list) (targetLine: int) : Comment option =
    attachedComments
    |> List.tryFind (fun ac ->
        ac.Attachment = Trailing && ac.Comment.Pos.Line = targetLine)
    |> Option.map (fun ac -> ac.Comment)

/// 모든 주석을 (줄 번호 -> 주석) 맵으로 변환
let buildCommentLineMap (comments: Comment list) : Map<int, Comment> =
    comments
    |> List.map (fun c -> (c.Pos.Line, c))
    |> Map.ofList
