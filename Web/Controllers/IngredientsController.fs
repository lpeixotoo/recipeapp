namespace recipeapp.Controllers

open Microsoft.AspNetCore.Mvc
open recipeapp

type IngredientView = { id: int; url: string; food: string }

[<Route "[controller]"; ApiController>]
type IngredientsController(service: Todo.Service) =
    inherit ControllerBase()

    let toProps (value : Todo.IngredientView) : Todo.IngredientProps = { food = value.food }

    member private this.WithUri(ingredient : Todo.IngredientView) : IngredientView =
        let url = this.Url.RouteUrl("GetIngredient", { id=ingredient.id }, this.Request.Scheme) // Supplying scheme is secret sauce for making it absolute as required by client
        { id = ingredient.id; url = url; food = ingredient.food }

    [<HttpGet>]
    member this.Get([<FromClientIdHeader>]clientId : ClientId) : Async<seq<IngredientView>> = async {
        let! ingredients = service.ListAllIngredients(clientId)
        return seq { for ingredient in ingredients -> this.WithUri(ingredient) }
    }

    [<HttpGet("{id}", Name="GetIngredient")>]
    member this.Get([<FromClientIdHeader>]clientId : ClientId, id) : Async<IActionResult> = async {
        let! ingredient = service.TryGetIngredient(clientId, id)
        return match ingredient with None -> this.NotFound() :> _ | Some ingredient -> ObjectResult(this.WithUri ingredient) :> _
    }

    [<HttpPost>]
    member this.Post([<FromClientIdHeader>]clientId : ClientId, [<FromBody>]value : Todo.IngredientView) : Async<IngredientView> = async {
        let! createdIngredient = service.CreateIngredient(clientId, toProps value)
        return this.WithUri createdIngredient
    }
