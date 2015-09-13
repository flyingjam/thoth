module Arguments
    (*
        Processes arguments
    *)

    type AuthorState = 
        |EmptyAuthor
        |Author of string

    type TitleState =
        |EmptyTitle
        |Title of string
    
    type CoverState =
        |EmptyCover
        |Cover of string

    type IndexState =
        |WebIndex
        |FileIndex

    type CLOptions = class
        val inputs : list<string>
        val file : bool
        val title : TitleState
        val author : AuthorState
        val cover : CoverState
        val index : IndexState

        new (input, file, title, author, cover, index) =
            {inputs = input; file = file; title = title; author = author; cover = cover; index = index}

        new (input, file, title, author, cover) =
            {inputs = input; file = file; title = title; author = author; cover = cover; index = FileIndex}

        new (input, file, title, author) =
            {inputs = input; file = file; title = title; author = author; cover = EmptyCover; index = FileIndex}
    
        new (input, (options : CLOptions)) =
            {inputs = input; file = options.file; title = options.title; author = options.author; cover = options.cover; index = options.index}

        member x.SetFile file =
            new CLOptions(x.inputs, file, x.title, x.author)
        
        member x.SetAuthor author =
            new CLOptions(x.inputs, x.file, x.title, author)

        member x.SetTitle title =
            new CLOptions(x.inputs, x.file, title, x.author)

        member x.SetCover cover =
            new CLOptions(x.inputs, x.file, x.title, x.author, cover)
    
        member x.SetIndex index =
            new CLOptions(x.inputs, x.file, x.title, x.author, x.cover, index)

        member x.AddInput input =
            new CLOptions((input :: x.inputs), x.file, x.title, x.author) 

    end
        
    let ProcessArguments args =
        let arglist = args |> List.ofSeq
        
        let rec loop arguments (acc : CLOptions) =
            match arguments with
            |[] -> acc
            |"-f" :: tl -> loop tl (acc.SetFile true)
            |"-w" :: tl -> loop tl (acc.SetIndex WebIndex)
            |"-c" :: tl ->
                    match tl with
                    |(x : string) :: xs -> loop xs (acc.SetCover (Cover(x)))
                    |_ -> loop tl acc
            |"-a" :: tl ->
                    match tl with
                    |(x : string) :: xs -> loop xs (acc.SetAuthor (Author(x)))
                    |_ -> loop tl acc
            |"-t" :: tl ->
                    match tl with
                    |(x : string) :: xs -> loop xs (acc.SetTitle (Title(x)))
                    |_ -> loop tl acc
            |hd :: tl -> loop tl (acc.AddInput hd)
            
        new CLOptions([], false, EmptyTitle, EmptyAuthor) |> loop arglist
