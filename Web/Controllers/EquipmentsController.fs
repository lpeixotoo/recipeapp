namespace RecipeApp.Controllers

open Microsoft.AspNetCore.Mvc
open RecipeApp

type FromClientIdHeaderAttribute() = inherit FromHeaderAttribute(Name="COMPLETELY_INSECURE_CLIENT_ID")

type GetByIdArgsTemplate = { id: int }

type EquipmentView = { id: int; url: string; tool: string }

[<Route "[controller]"; ApiController>]
type EquipmentsController(service: Recipe.Service) =
    inherit ControllerBase()

    let toProps (value : Recipe.EquipmentView) : Recipe.EquipmentProps = { tool = value.tool }

    member private this.WithUri(equipment : Recipe.EquipmentView) : EquipmentView =
        let url = this.Url.RouteUrl("GetEquipment", { id=equipment.id }, this.Request.Scheme) // Supplying scheme is secret sauce for making it absolute as required by client
        { id = equipment.id; url = url; tool = equipment.tool }

    [<HttpGet>]
    member this.Get([<FromClientIdHeader>]clientId : ClientId) : Async<seq<EquipmentView>> = async {
        let! equipments = service.ListAllEquipments(clientId)
        return seq { for equipment in equipments -> this.WithUri(equipment) }
    }

    [<HttpGet("{id}", Name="GetEquipment")>]
    member this.Get([<FromClientIdHeader>]clientId : ClientId, id) : Async<IActionResult> = async {
        let! equipment = service.TryGetEquipment(clientId, id)
        return match equipment with None -> this.NotFound() :> _ | Some equipment -> ObjectResult(this.WithUri equipment) :> _
    }

    [<HttpPost>]
    member this.Post([<FromClientIdHeader>]clientId : ClientId, [<FromBody>]value : Recipe.EquipmentView) : Async<EquipmentView> = async {
        let! createdEquipment = service.CreateEquipment(clientId, toProps value)
        return this.WithUri createdEquipment
    }
