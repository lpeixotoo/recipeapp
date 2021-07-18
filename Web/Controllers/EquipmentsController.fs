namespace recipeapp.Controllers

open Microsoft.AspNetCore.Mvc
open recipeapp

type FromClientIdHeaderAttribute() = inherit FromHeaderAttribute(Name="COMPLETELY_INSECURE_CLIENT_ID")

type GetByIdArgsTemplate = { id: int }

[<Route "[controller]"; ApiController>]
type EquipmentsController(service: Todo.Service) =
    inherit ControllerBase()

    let toProps (value : Todo.EquipmentView) : Todo.EquipmentProps = { tool = value.tool }

    [<HttpGet>]
    member this.Get([<FromClientIdHeader>]clientId : ClientId) : Async<seq<Todo.EquipmentView>> = async {
        let! equipments = service.ListAllEquipments(clientId)
        return equipments
    }

    [<HttpPost>]
    member this.Post([<FromClientIdHeader>]clientId : ClientId, [<FromBody>]value : Todo.EquipmentView) : Async<Todo.EquipmentView> = async {
        let! createdEquipment = service.CreateEquipment(clientId, toProps value)
        return createdEquipment
    }
