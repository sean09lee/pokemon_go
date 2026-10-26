# Copilot Instructions

## Project Guidelines
- In the pokemon_go TeamBuilder Blazor app, shared cross-page state (like uploaded Pokémon data) should be stored in scoped state services (e.g., PokemonUploadState) rather than duplicated in page-local fields, so multiple pages/nav tabs can access the same data.
- In the pokemon_go repository, never modify the main README.md unless explicitly instructed. Documentation/content changes should go into separate markdown files (e.g., under docs/) instead.

## Code Style
- Use spaces instead of tabs for indentation in .razor files.