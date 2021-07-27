module RecipeApp.Recipe

let [<Literal>] Category = "Recipes"
/// Maps a ClientId to the StreamName where data for that client will be held
let streamName (clientId: ClientId) = FsCodec.StreamName.create Category (ClientId.toString clientId)

[<AutoOpen>]
module Types =
    type Equipment        = { id: int; tool: string }
    type Ingredient       = { id: int; food: string }
    type RecipeEquipment  = { equipment: Equipment; quantity: int }
    type RecipeIngredient = { ingredient: Ingredient; quantity: string; unit: string }
    type Recipe           = { id: int; name: string; description: string; ingredients: RecipeIngredient list; equipments: RecipeEquipment list }

// NB - these types and the union case names reflect the actual storage formats and hence need to be versioned with care
module Events =

    type ClearedData = { nextRecipeId: int; nextIngredientId: int; nextEquipmentId: int }
    type SnapshottedData = {
        recipes : Recipe list;
        ingredients: Ingredient list;
        equipments: Equipment list;
        nextRecipeId: int;
        nextIngredientId: int;
        nextEquipmentId: int;
    }
    /// Events we keep in Recipes-* streams
    type Event =
        | AddedIngredient of Ingredient
        | AddedEquipment of Equipment
        | AddedRecipe of Recipe
        | UpdatedRecipe of Recipe
        | Cleared of ClearedData
        | Snapshotted of SnapshottedData
        interface TypeShape.UnionContract.IUnionContract
    let codec = FsCodec.NewtonsoftJson.Codec.Create<Event>()

/// Types and mapping logic used maintain relevant State based on Events observed on the Recipe Stream
module Fold =

    /// Present state of the Recipe as inferred from the Events we've seen to date
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
        | Events.Cleared         e          -> {
            recipes = [];
            ingredients = [];
            equipments = [];
            nextRecipeId = e.nextRecipeId;
            nextIngredientId = e.nextIngredientId;
            nextEquipmentId= e.nextEquipmentId }
        | Events.Snapshotted     s          -> {
            recipes = s.recipes;
            ingredients = s.ingredients;
            equipments = s.equipments;
            nextRecipeId = s.nextRecipeId;
            nextIngredientId = s.nextIngredientId;
            nextEquipmentId= s.nextEquipmentId }
    /// Folds a set of events from the store into a given `state`
    let fold : State -> Events.Event seq -> State = Seq.fold evolve
    /// Determines whether a given event represents a checkpoint that implies we don't need to see any preceding events
    let isOrigin = function Events.Cleared _ | Events.Snapshotted _ -> true | _ -> false
    /// Prepares an Event that encodes all relevant aspects of a State such that `evolve` can rehydrate a complete State from it
    let snapshot state = Events.Snapshotted {
            recipes = state.recipes;
            ingredients = state.ingredients;
            equipments = state.equipments;
            nextRecipeId = state.nextRecipeId;
            nextIngredientId = state.nextIngredientId;
            nextEquipmentId= state.nextEquipmentId }

/// Properties that can be edited on a Recipe item
type EquipmentProps   = { tool: string }
type IngredientProps  = { food: string }
type RecipeProps      = { name: string; description: string; ingredients: RecipeIngredient list; equipments: RecipeEquipment list }

/// Defines the operations a caller can perform on a Recipe
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

/// Views
type EquipmentView = Equipment
type IngredientView = Ingredient
type RecipeView = Recipe

/// Defines operations that a Controller can perform on a Recipe App
type Service internal (resolve : ClientId -> Equinox.Decider<Events.Event, Fold.State>) =

    let query clientId projection =
        let decider = resolve clientId
        decider.Query projection
    let handleEquipment clientId command =
        let decider = resolve clientId
        decider.Transact(fun state ->
            let events = interpret command state
            let state' = Fold.fold state events
            state'.equipments, events)
    let handleIngredient clientId command =
        let decider = resolve clientId
        decider.Transact(fun state ->
            let events = interpret command state
            let state' = Fold.fold state events
            state'.ingredients, events)
    let handleRecipe clientId command =
        let decider = resolve clientId
        decider.Transact(fun state ->
            let events = interpret command state
            let state' = Fold.fold state events
            state'.recipes, events)

    let existsIngredient (ingredientId: int) (ingredients: RecipeIngredient list) : bool =
        ingredients |> List.exists (fun { ingredient = ingredient } -> ingredient.id = ingredientId)

    (* READ *)
    member _.ListAllEquipments clientId  : Async<EquipmentView seq> =
        query clientId (fun state -> seq { for equipment in state.equipments -> equipment })

    member _.ListAllIngredients clientId  : Async<IngredientView seq> =
        query clientId (fun state -> seq { for ingredient in state.ingredients -> ingredient })

    member _.ListAllRecipes clientId  : Async<RecipeView seq> =
        query clientId (fun state -> seq { for recipe in state.recipes -> recipe })

    member _.TryGetEquipment(clientId, equipmentId)  : Async<EquipmentView option> =
        query clientId (fun state -> state.equipments |> List.tryFind(fun equipment -> equipment.id = equipmentId))

    member _.TryGetIngredient(clientId, ingredientId)  : Async<IngredientView option> =
        query clientId (fun state -> state.ingredients |> List.tryFind(fun ingredient -> ingredient.id = ingredientId))

    member _.TryGetRecipe(clientId, recipeId)  : Async<RecipeView option> =
        query clientId (fun state -> state.recipes |> List.tryFind(fun recipe -> recipe.id = recipeId))

    member _.ListPaginatedItems<'T>(pageSize : int) (pageNumber : int) (itemList : list<'T>) =
        let chunckedList = itemList |> List.chunkBySize pageSize
        /// Does not allow pagination overflow
        let page = match (pageNumber > chunckedList.Length) with
                   | true -> chunckedList.Length
                   | false -> pageNumber

        match chunckedList.IsEmpty with
        | true -> List.empty<'T>
        | false ->
            chunckedList
            |> List.skip (page - 1)
            |> List.head

    member _.ListRecipesPerIngredient(clientId, ingredientId)  : Async<RecipeView list> =
        query clientId (fun state ->
            state.recipes
            |> List.filter (fun { ingredients = recipeIngredients } -> recipeIngredients |> existsIngredient ingredientId)
        )

    (* WRITE-READ *)
    /// Create a new equipment; response contains the generated `id`
    member _.CreateEquipment(clientId, equipmentTemplate: EquipmentProps) : Async<EquipmentView> = async {
        let! state' = handleEquipment clientId (AddEquipment equipmentTemplate)
        return List.head state' }
    /// Create a new ingredient; response contains the generated `id`
    member _.CreateIngredient(clientId, ingredientTemplate: IngredientProps) : Async<IngredientView> = async {
        let! state' = handleIngredient clientId (AddIngredient ingredientTemplate)
        return List.head state' }
    /// Create a new recipe; response contains the generated `id`
    member _.CreateRecipe(clientId, recipeTemplate: RecipeProps) : Async<RecipeView> = async {
        let! state' = handleRecipe clientId (AddRecipe recipeTemplate)
        return List.head state' }
    /// Update the specified recipe as referenced by the `recipe.id`
    member _.PatchRecipe(clientId, id: int, updatedRecipe: RecipeProps) : Async<RecipeView> = async {
        let! state' = handleRecipe clientId (UpdateRecipe (id, updatedRecipe))
        return state' |> List.find (fun x -> x.id = id)}

let create resolveStream =
    let resolve = streamName >> resolveStream >> Equinox.createDecider
    Service(resolve)
