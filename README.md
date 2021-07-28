# Recipe App

The Recipe App is an attempt to leverage event source + MVC architectures to implement a CMS application
with recipes, ingredients and equipments as entities.

# How to run

To run a local instance of the Website on https://localhost:5001 and http://localhost:5000

    dotnet run -p Web

# API Usage

A set of postman/insomnia collection could be found at docs/collections folder.

But here's an API overview:

|              Description               |  Method | Endpoint  | Body  |
|:-:|:-:|-|:-:|
| Get All Equipments                     |  `GET`  | `/equipment`                                             | - |
| Get Equipment by ID                    |  `GET`  | `/equipment/<id>`                                        | - |
| Get All Ingredients                    |  `GET`  | `/ingredients`                                           | - |
| Get Ingredient by ID                   |  `GET`  | `/ingredients/<id>`                                      | - |
| Get All Recipes                        |  `GET`  | `/recipes`                                               | - |
| Get Recipe by ID                       |  `GET`  | `/recipes/<id>`                                          | - |
| Get Recipes By Ingredient (Paginated)  |  `GET`  | `/recipes/ingredient/<ingredient_id>?page=<page_number>` | - |
| Create Equipment                       |  `POST` | `/equipment`                                             | `{ tool: <string> }` |
| Create Ingredient                      |  `POST` | `/ingredient`                                            | `{ food: <string> }` |
| Create Recipe                          |  `POST` | `/recipes`                                               | `{ tool: <string> }` |
| Update Recipe                          | `PATCH` | `/recipes/<id>`                                          | `{ food: <string> }` |

# Roadmap
