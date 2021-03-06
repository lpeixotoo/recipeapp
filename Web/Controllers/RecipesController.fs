namespace RecipeApp.Controllers

open Microsoft.AspNetCore.Mvc
open RecipeApp

type RecipeView = {
    id: int;
    url: string;
    name: string;
    description: string;
    ingredients: Recipe.Types.RecipeIngredient list;
    equipments: Recipe.Types.RecipeEquipment list }

[<Route "[controller]"; ApiController>]
type RecipesController(service: Recipe.Service) =
    inherit ControllerBase()

    let toProps (value : Recipe.RecipeView) : Recipe.RecipeProps = {
        name =  value.name;
        description = value.description;
        ingredients = value.ingredients;
        equipments = value.equipments }

    let [<Literal>] PageSize = 8

    member private this.WithUri(recipe : Recipe.RecipeView) : RecipeView =
        let url = this.Url.RouteUrl("GetRecipe", { id=recipe.id }, this.Request.Scheme) // Supplying scheme is secret sauce for making it absolute as required by client
        { id = recipe.id; url = url; name = recipe.name; description = recipe.description; ingredients = recipe.ingredients; equipments = recipe.equipments }

    [<HttpGet>]
    member this.Get([<FromClientIdHeader>]clientId : ClientId) : Async<seq<RecipeView>> = async {
        let! recipes = service.ListAllRecipes(clientId)
        return seq { for recipe in recipes -> this.WithUri(recipe) }
    }

    [<HttpGet("{id}", Name="GetRecipe")>]
    member this.Get([<FromClientIdHeader>]clientId : ClientId, id) : Async<IActionResult> = async {
        let! recipe = service.TryGetRecipe(clientId, id)
        return match recipe with None -> this.NotFound() :> _ | Some recipe -> ObjectResult(this.WithUri recipe) :> _
    }

    [<HttpGet "ingredient/{ingredientId:int}">]
    member this.GetRecipesByIngredient([<FromClientIdHeader>]clientId : ClientId, ingredientId, [<FromQueryAttribute>]page: int) : Async<list<RecipeView>> = async {
        let! recipes = service.ListRecipesPerIngredient(clientId, ingredientId)
        // default page value
        let pageNumber = match page = 0 with
                         | true -> 1
                         | _ -> page

        return recipes
        |> service.ListPaginatedItems<Recipe.RecipeView> PageSize 1
        |> List.map (function recipe -> this.WithUri(recipe))
    }

    [<HttpPost>]
    member this.Post([<FromClientIdHeader>]clientId : ClientId, [<FromBody>]value : Recipe.RecipeView) : Async<RecipeView> = async {
        let! createdRecipe = service.CreateRecipe(clientId, toProps value)
        return this.WithUri createdRecipe
    }

    [<HttpPatch "{id}">]
    member this.Patch([<FromClientIdHeader>]clientId : ClientId, id, [<FromBody>]value : Recipe.RecipeView) : Async<RecipeView> = async {
        let! updatedRecipe = service.PatchRecipe(clientId, id, toProps value)
        return this.WithUri updatedRecipe
    }
