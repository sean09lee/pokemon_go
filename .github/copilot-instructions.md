# Copilot Instructions

## Project Guidelines
- In the pokemon_go TeamBuilder Blazor app, shared cross-page state (like uploaded Pokémon data) should be stored in scoped state services (e.g., PokemonUploadState) rather than duplicated in page-local fields, so multiple pages/nav tabs can access the same data.