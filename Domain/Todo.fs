module recipeapp.Todo

let [<Literal>] Category = "Recipes"
/// Maps a ClientId to the StreamName where data for that client will be held
let streamName (clientId: ClientId) = FsCodec.StreamName.create Category (ClientId.toString clientId)

[<AutoOpen>]
module Types =
    type Ingredient       = { id: int; food: string }
    type Equipment        = { id: int; tool: string }
    type RecipeIngredient = { ingredient: Ingredient; quantity: string; unit: string }
    type RecipeEquipment  = { equipment: Equipment; quantity: int }
    type Recipe           = { id: int; name: string; description: string; ingredients: RecipeIngredient list; equipments: RecipeEquipment list }

// NB - these types and the union case names reflect the actual storage formats and hence need to be versioned with care
module Events =

    type ItemData =     { id : int; order : int; title : string; completed : bool }
    type DeletedData =  { id : int }
    type ClearedData =  { nextId : int }
    type SnapshotData = { nextId : int; items : ItemData[] }

    /// Events we keep in Recipes-* streams
    type Event =
        | AddedIngredient of Ingredient
        | AddedEquipment of Equipment
        | AddedRecipe of Recipe
        | UpdatedRecipe of Recipe
        interface TypeShape.UnionContract.IUnionContract
    let codec = FsCodec.NewtonsoftJson.Codec.Create<Event>()

/// Types and mapping logic used maintain relevant State based on Events observed on the Todo List Stream
module Fold =

    /// Present state of the Todo List as inferred from the Events we've seen to date
    type State = {
        recipes : Recipe list;
        ingredients: Ingredient list;
        equipments: Equipment list;
        nextRecipeId: int;
        nextIngredientId: int;
        nextEquipmentId: int;
    }
    /// State implied by the absence of any events on this stream
    let initial = {
        recipes = [];
        ingredients = [];
        equipments = [];
        nextRecipeId = 0;
        nextIngredientId = 0;
        nextEquipmentId = 0;
    }
    /// Compute State change implied by a given Event
    let evolve state = function
        | Events.AddedEquipment  equipment  -> { state with equipments = equipment :: state.equipments; nextEquipmentId = state.nextEquipmentId + 1 }
        | Events.AddedIngredient ingredient -> { state with ingredients = ingredient :: state.ingredients; nextIngredientId = state.nextIngredientId + 1 }
        | Events.AddedRecipe     recipe     -> { state with recipes = recipe :: state.recipes; nextRecipeId = state.nextRecipeId + 1 }
        | Events.UpdatedRecipe   recipe     -> { state with recipes = state.recipes |>  List.map (function { id = id } when id = recipe.id -> recipe | item -> item) }
    /// Folds a set of events from the store into a given `state`
    let fold : State -> Events.Event seq -> State = Seq.fold evolve

/// Properties that can be edited on a Todo List item
type RecipeProps      = { name: string; description: string; ingredients: RecipeIngredient list; equipments: RecipeEquipment list }
type IngredientProps  = { food: string }
type EquipmentProps   = { tool: string }

/// Defines the operations a caller can perform on a Todo List
type Command =
    /// Create a single equipment
    | AddEquipment of EquipmentProps
    /// Create a single ingredient
    | AddIngredient of IngredientProps
    /// Create a single recipe
    | AddRecipe of RecipeProps
    /// Update a single recipe
    | UpdateRecipe of id: int * RecipeProps

/// Defines the decision process which maps from the intent of the `Command` to the `Event`s that represent that decision in the Stream
let interpret command (state : Fold.State) =
    /// entity maker function
    let mkEquipment id (value : EquipmentProps) : Equipment = { id = id; tool = value.tool }
    let mkIngredient id (value : IngredientProps) : Ingredient = { id = id; food =  value.food }
    let mkRecipe id (value : RecipeProps) : Recipe =
        {
            id = id;
            name= value.name;
            description = value.description;
            ingredients = value.ingredients;
            equipments = value.equipments
        }

    match command with
    | AddEquipment equipment -> [Events.AddedEquipment (mkEquipment state.nextEquipmentId equipment )]
    | AddIngredient ingredient -> [Events.AddedIngredient (mkIngredient state.nextIngredientId ingredient)]
    | AddRecipe recipe -> [Events.AddedRecipe (mkRecipe state.nextRecipeId recipe)]
    | UpdateRecipe (recipeId, recipe) ->
        let updatedRecipe = mkRecipe recipeId recipe
        match state.recipes |> List.tryFind (function {id = id} -> id = recipeId) with
        | Some currentRecipe when currentRecipe <> updatedRecipe -> [Events.UpdatedRecipe updatedRecipe]
        | _ -> []

/// A single Item in the Todo List
type View = { id: int; order: int; title: string; completed: bool }

/// Defines operations that a Controller can perform on a Todo List
type Service internal (resolve : ClientId -> Equinox.Decider<Events.Event, Fold.State>) =

    let execute clientId command =
        let decider = resolve clientId
        decider.Transact(interpret command)
    let query clientId projection =
        let decider = resolve clientId
        decider.Query projection
    let handle clientId command =
        let decider = resolve clientId
        decider.Transact(fun state ->
            let events = interpret command state
            let state' = Fold.fold state events
            state'.items, events)

    let render (item: Events.ItemData) : View =
        {   id = item.id
            order = item.order
            title = item.title
            completed = item.completed }

    (* READ *)

    /// List all open items
    member _.List clientId  : Async<View seq> =
        query clientId (fun x -> seq { for x in x.items -> render x })

    /// Load details for a single specific item
    member _.TryGet(clientId, id) : Async<View option> =
        query clientId (fun x -> x.items |> List.tryFind (fun x -> x.id = id) |> Option.map render)

    (* WRITE *)

    /// Execute the specified (blind write) command
    member _.Execute(clientId , command) : Async<unit> =
        execute clientId command

    (* WRITE-READ *)

    /// Create a new ToDo List item; response contains the generated `id`
    member _.Create(clientId, template: Props) : Async<View> = async {
        let! state' = handle clientId (Add template)
        return List.head state' |> render }

    /// Update the specified item as referenced by the `item.id`
    member _.Patch(clientId, id: int, value: Props) : Async<View> = async {
        let! state' = handle clientId (Update (id, value))
        return state' |> List.find (fun x -> x.id = id) |> render}

let create resolveStream =
    let resolve = streamName >> resolveStream >> Equinox.createDecider
    Service(resolve)
