namespace recipeapp.Controllers

open Microsoft.AspNetCore.Mvc
open recipeapp

[<Route "[controller]"; ApiController>]
type IngredientsController(service: Todo.Service) =
    inherit ControllerBase()

    let toProps (value : Todo.IngredientView) : Todo.IngredientProps = { food = value.food }

    [<HttpGet>]
    member this.Get([<FromClientIdHeader>]clientId : ClientId) : Async<seq<Todo.IngredientView>> = async {
        let! ingredients = service.ListAllIngredients(clientId)
        return ingredients
    }

    [<HttpPost>]
    member this.Post([<FromClientIdHeader>]clientId : ClientId, [<FromBody>]value : Todo.IngredientView) : Async<Todo.IngredientView> = async {
        let! createdIngredient = service.CreateIngredient(clientId, toProps value)
        return createdIngredient
    }
