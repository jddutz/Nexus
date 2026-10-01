# Game System Lifecycle

## Scope

`GameSystem` owns lifecycle traversal for the active scene. Every frame, it performs one parent-first traversal of the active scene, its game-object hierarchy, and each game object's components. The scene itself is initialized and activated when it becomes current, and deactivated when it stops being current.

## Frame Traversal

The traversal visits the scene first, then each game object, its components, and its child scene nodes. Thus, `Scene.Update(deltaTime)` runs exactly once at the start of each frame traversal, before any dependents. For each entity visited:

1. Call `Initialize` if the entity has not been initialized.
2. If the entity is not activated and is eligible, call `Activate`.
3. Call `Update(deltaTime)` only if the entity is activated after the activation step.

An inactive entity is considered for activation again on each subsequent frame. Its initialization is not repeated. An entity is eligible for activation only when `CanActivate() == true`. `CanActivate()` is checked only for an entity that is not already activated; returning `false` does not deactivate an active entity or prevent it from updating.

An inactive parent blocks activation and updates for all of its dependents, including components and descendant game objects, until the parent is active. Initialization is not blocked by parent activation state: entities in the active scene hierarchy are initialized when necessary even under an inactive parent.

## Entity Hooks

`Initialize`, `Activate`, `Update`, and `Deactivate` remain virtual entity-level hooks. The game system invokes them on the entity being processed; the hooks operate on that entity and do not recursively invoke lifecycle methods on its components or children. The game system owns traversal and ordering.

## Deactivation and Removal

Deactivation runs dependents before their parents. For a game object, its components and descendant subtrees are deactivated before the game object. When a scene is deactivated, its game-object dependents are deactivated before the scene itself.

Removing a component deactivates that component. Removing a game object or other scene subtree deactivates the removed subtree, with dependents processed before their parents. Removed entities are not updated after removal from the active hierarchy.

## Transfers

Moving an entity or subtree between managed hierarchies preserves its initialization state. The source hierarchy deactivates it before the destination hierarchy activates it. Destination activation follows normal parent-first eligibility rules; initialization is skipped when the entity is already initialized.

## Collection Changes During Traversal

Lifecycle callbacks may add or remove components or scene nodes while traversal is in progress. Traversal must use safe iteration and verify that each snapshotted entity still belongs to the active hierarchy before processing or updating it. It must never update an entity before it has been initialized and activated. Additions made during a callback may be deferred until the next frame. Removals still require deactivation of the removed entity or subtree, and removed entities must not receive later updates from the in-progress traversal, even if re-added before that traversal finishes.