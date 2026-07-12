# Target Solution

The target solution is defined normatively by:

- `bundle://architecture/01-csharp-boundary-map.md`
- `bundle://architecture/02-csharp-dependency-direction.md`
- `bundle://architecture/05-filebrowser-contract.md`
- `bundle://architecture/06-fileinteraction-design.md`
- `bundle://architecture/07-candoitall-integration.md`
- `bundle://architecture/08-cache-and-invalidation.md`
- `bundle://architecture/09-ui-assets-and-layout.md`
- `bundle://architecture/10-transfer-and-packaging.md`

FileTools ships independent contracts, browser, interaction, optional adapters/renderers, tests, and Sandbox. Components relinquishes FileBrowser ownership after proof. CanDoItAll remains unchanged and receives an implementation-ready future integration design.

