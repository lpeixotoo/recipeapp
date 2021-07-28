# Recipe App

The Recipe App is an attempt to leverage event source + MVC architectures to implement a CMS application
with recipes, ingredients and equipments as entities.

# How to run

To run a local instance of the Website on https://localhost:5001 and http://localhost:5000

    dotnet run -p Web

# API Usage

A set of postman/insomnia collection could be found at [folder](/docs/collections).

Here's an API overview:

|              Description               |  Method | Endpoint  | Body  |
|:-:|:-:|-|:-:|
| <td colspan=4> Read Methods </td>
| Get All Equipments                     |  `GET`  | `/equipment`                                             | - |
| Get Equipment by ID                    |  `GET`  | `/equipment/<id>`                                        | - |
| Get All Ingredients                    |  `GET`  | `/ingredients`                                           | - |
| Get Ingredient by ID                   |  `GET`  | `/ingredients/<id>`                                      | - |
| Get All Recipes                        |  `GET`  | `/recipes`                                               | - |
| Get Recipe by ID                       |  `GET`  | `/recipes/<id>`                                          | - |
| Get Recipes By Ingredient (Paginated)  |  `GET`  | `/recipes/ingredient/<ingredient_id>?page=<page_number>` | - |
| <td colspan=4> Write Methods </td>
| Create Equipment                       |  `POST` | `/equipment`                                             | [schema](/docs/schemas/equipmentschema.json) |
| Create Ingredient                      |  `POST` | `/ingredient`                                            | [schema](/docs/schemas/ingredientschema.json)|
| Create Recipe                          |  `POST` | `/recipes`                                               | [schema](/docs/schemas/recipeschema.json) |
| Update Recipe                          | `PATCH` | `/recipes/<id>`                                          | [schema](/docs/schemas/recipeschema.json) |

# Roadmap

We've added a Roadmap [Projects](https://github.com/lpeixotoo/recipeapp/projects) to help us tracking progresses through issues and pull requests.

# Learn more

- [Equinox](https://github.com/jet/equinox) -- .NET Event Sourcing Library
- [Propulsion](https://github.com/jet/propulsion) -- .NET event stream projection and scheduling platform
- Event Sourcing
  - [Martin Fowler's article](https://martinfowler.com/eaaDev/EventSourcing.html)
  - [Microservice.io article](https://microservices.io/patterns/data/event-sourcing.html)