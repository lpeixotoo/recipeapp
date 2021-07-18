namespace recipeapp.Controllers

open Microsoft.AspNetCore.Mvc
open recipeapp

[<Route "[controller]"; ApiController>]
type RecipesController(service: Todo.Service) =
    inherit ControllerBase()

    let toProps (value : Todo.RecipeView) : Todo.RecipeProps = {
        name =  value.name;
        description = value.description;
        ingredients = value.ingredients;
        equipments = value.equipments }

    [<HttpGet>]
    member this.Get([<FromClientIdHeader>]clientId : ClientId) : Async<seq<Todo.RecipeView>> = async {
        let! recipes = service.ListAllRecipes(clientId)
        return recipes
    }

    [<HttpPost>]
    member this.Post([<FromClientIdHeader>]clientId : ClientId, [<FromBody>]value : Todo.RecipeView) : Async<Todo.RecipeView> = async {
        let! createdRecipe = service.CreateRecipe(clientId, toProps value)
        return createdRecipe
    }

    [<HttpPatch "{id}">]
    member this.Patch([<FromClientIdHeader>]clientId : ClientId, id, [<FromBody>]value : Todo.RecipeView) : Async<Todo.RecipeView> = async {
        let! updatedRecipe = service.PatchRecipe(clientId, id, toProps value)
        return updatedRecipe
    }
