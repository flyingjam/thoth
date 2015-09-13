module Process
    open NSoup
    open System.IO
    open System
    open Monads
    open Parse
    open Download
    open System.Text
    open Epub

    type ProcessedImages = class
        (*
            Class that represents the result from processing the images
            Consist of a list of the original sources to be used for downloading
            and a list of filepaths to what should be downloaded
        *)

        //TODO Validate that original sources and filepaths are of the same length
        val originalSources : list<string>
        val filepaths: list<string>
        
        new (original, files) =
            {originalSources = original; filepaths = files}
    end


    type Page = class
        (*
            Class that represents an entire page
            The constructor is useless, constuction is actually handled by
            ProcessPage
        *)
        val url : string
        val title : string
        val html : string
        val uuid : string
        val images : ProcessedImages

        new (url, title, html, uuid, images) =
            {url = url; title = title; html = html; uuid = uuid; images = images;}
    end
    
     
    
    let GetImage (parent : NSoup.Nodes.Element) =
        (*
            Helper function that wraps around Children.Select("img")
        *)
        try
            Some (parent.Children.Select("img"))
        with
        |_ -> None

    let ProcessImages url title images =
        (*
            This function is terrible and not pure at all but
            I don't want to make deep copies of everything. So blame
            .net object references.
        *)
        
        let originalSources = images |> Seq.toList 
                            |> List.map (fun (x : NSoup.Nodes.Element) -> x.Attr("abs:src"))

        let UUID = Guid.NewGuid().ToString("N").Substring(0, 7)
        let identifier = UUID 
        let rec loop counter (acc : list<string>) (im : list<NSoup.Nodes.Element>) = 
            if (im = []) then
                acc
            else
                let head = im |> List.head
                let extension = GetExtension (head.Attr("src"))
                let filename = identifier + (string counter) + extension
            
                head.Attr("src", "../Images/" + filename) |> ignore
                loop (counter + 1) (((CreateRelativePath "temp/OEBPS/Images/") + filename) :: acc) (List.tail im)

        let filepaths = loop 0 [] (images |> Seq.toList)
        new ProcessedImages(originalSources, filepaths)
     
    let GetURLs url =
        (*
            Returns a sequence of all anchor tags
        *)
        let doc = NSoupDownload url
        match doc with
        |Some(x) -> Some (x.Body.Select("a"))
        |None -> None

    let ProcessPage url =
        (*
            Uses a Maybe monad (defined in Monads.fs) to download the file, get the title,
            get the content, get the parent tag, process the images, and return a new
            Page object.
            If downloading the file, getting the content, or getting the parent tag fails, then
            the entire operation fails
            GetTitle cannot fail and images will default to an empty image source
        *)
        let maybe = new OptionBuilder() 
        
        maybe{
            let! doc = NSoupDownload url
            let title = GetTitle doc
            let! content = FindContent doc
            let! parent = content |> ParentByStringContent
            let imageSources = GetImage parent
            let id = Guid.NewGuid().ToString("N")
            let images = 
                match imageSources with
                |Some x -> ProcessImages url title x
                |None -> new ProcessedImages([], [])

            return (new Page(url, title, (parent.Html()), id, images))
        }

    let DownloadPage (page : Page) =
        (*
            Takes a page, writes the html and downloads the images. 
        *)

        //Create neccesary file structure
        //Directory.CreateDirectory checks to see if the path exists first
        //so no overhead
        Directory.CreateDirectory (CreateRelativePath "temp") |> ignore
        Directory.CreateDirectory (CreateRelativePath "temp/OEBPS") |> ignore
        Directory.CreateDirectory (CreateRelativePath "temp/OEBPS/Text") |> ignore
        Directory.CreateDirectory (CreateRelativePath "temp/OEBPS/Images") |> ignore
        Directory.CreateDirectory (CreateRelativePath "temp/META-INF") |> ignore

        //Write html
        WriteXHTML page.title page.html ((CreateRelativePath "temp/OEBPS/Text/") + page.uuid + ".xhtml")
        //Download images, stuff
        let (images : ProcessedImages) = page.images
        printfn "Downloading images for %s" page.title
        (List.zip images.originalSources images.filepaths) |> List.map (fun x ->
            match x with
            |(a, b) -> ImageDownload a b)

    let ProcessList urls =
        let pages = urls |> MaybeMap (fun x -> ProcessPage x)
        //This is not actually pmap
        pmap (fun x -> DownloadPage x) pages |> ignore
        pages 
    
    let EbookFromList title author cover urls =
        (*
            "Complete" function - takes an title, author, cover and a list of urls.
            Downloads all the urls and creates an epub.
        *)

        let pages = ProcessList urls
        let book = GetBook |> AddTitle title |> AddAuthor author |>AddCover cover
        let html = pages |> List.rev |> List.fold (fun (acc : Book) (page : Page) ->
            acc |> AddHTML ((CreateRelativePath "temp/OEBPS/Text/") + page.uuid + ".xhtml") page.title) book
        let img = pages |> List.fold (fun (acc : Book) (page : Page) ->
            page.images.filepaths |> List.fold (fun ac img ->
                ac |> AddImage img) acc) html
        CreateEpub img
             
    
