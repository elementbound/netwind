# mplay-test

A test project to figure out how to reliably sync movement between multiple clients in a server-authorative setup.

The project uses [Unity Netcode for GameObjects](https://docs-multiplayer.unity3d.com/docs/getting-started/about). The
concepts should be transferrable to almost any of the major networking packages for Unity - in particular I've seen
Mirror to be quite similar.

## How to run

To test multiple instances running of the same project, [ParrelSync](https://github.com/VeriorPies/ParrelSync) is used.
After downloading the project, you can create a clone, open it in a separate editor and run one of the instances as
host/server and the other as client.

## How it works

Since the focus is just player movement, the code itself is not very expansive, with the key components being:

* [PlayerMovementController.cs](Assets/Scripts/PlayerMovementController.cs)
  * Updates player state based on player input. Also handles client-side prediction and server reconciliation.
* [InputProvider.cs](Assets/Scripts/InputProvider.cs)
  * Gathers input from the local player.
* [TickHistoryBuffer.cs](Assets/Scripts/TickHistoryBuffer.cs)
  * Ring-buffer to store whatever data ( in this case player state and input ) associated with any given network tick.

### Base flow

1. Clients gather their inputs ([InputProvider.cs](Assets/Scripts/InputProvider.cs))
1. Input is submitted to the server ([PlayerMovementController.cs](Assets/Scripts/PlayerMovementController.cs))
   1. Each client only submits their own respective input
   1. Each client updates their local game state based on their own inputs
1. The server receives inputs from the clients and batches them until the next network tick
1. On the next network tick, the server rolls back the game state to the earliest input received
   1. i.e. if an input was received 7 ticks ago, that's where the server will roll back
1. The server then resimulates the game state with the received inputs
   1. Each state is sent to the clients so they are up-to-date
1. Whenever the client receives an authorative state from the server
   1. Updates its local cache
   1. Resimulates from the last received state to the current tick, based on known inputs

Inbetween network ticks, entity states are interpolated between t-2 and t-1 - i.e. to the previous frame, from the one
before that. This is done since most of the time we don't have an authorative state for the current frame, but we might
have one for the previous one. While we interpolate between t-2 and t-1, we have some time to receive t-0.

In theory this should work perfectly, since both the server and client runs the exact same simulation*. The purpose of
this whole flow is to avoid having the client to wait for the server's response on the local player's actions - if the
player presses a button, they expect instant response, which is impossible with any latency higher than what's expected
on LAN.

So this way the client can 'predict' the server's response, and then adjust their own game state based on the server's
reply.

## Expectations

Movement should feel fluid and consistent at 50ms latency. This is the best case scenario, where everything should work
well.

Movement should work and be somewhat reliable at 200ms latency. This is the worst case scenario, where we're content if
it's playable, even if just barely.

## Known issues

### Inconsistent movement

See video:

[![Inconsistent movement](https://img.youtube.com/vi/3wPXBLMyq88/0.jpg)](https://www.youtube.com/watch?v=3wPXBLMyq88)

While moving around, the local player occasionally jumps about. This seems to be more pronounced during frequent and
sudden direction changes ( i.e. changing inputs ).

Seemingly the local simulation somewhat differs from the authorative simulation majorly. I do not think this is due to
floating point representation, since the jumps can be more than 0.25 units on a single frame.

My best guess would be the inputs, which - in theory - also shouldn't be an issue, since both the server and the client
work with the same sampled inputs.

### Broken entity interpolation

See video:

[![Broken entity interpolation](https://img.youtube.com/vi/5N-sNvDxe50/0.jpg)](https://www.youtube.com/watch?v=5N-sNvDxe50)

While moving an entity locally, its motion is smooth. However, that same movement, on other clients, appear choppy, as
if no interpolation was done.

I suspect that the issue is that the interpolation is done based on the current frame, and not based on the last frames
received from the server.

## Known limitations

### Collisions

No collisions are considered at the moment, for two reasons:

1. When resimulating from a given frame, only the current object is rolled back, not the whole world. This would mean
   inaccurate collisions during client-side prediction, since while the current object is in the past, all other objects
   are in the present, which would result in ghost collisions.
1. I've tried using Unity's built-in `CharacterController`. For some reason the objects didn't move on consistent speeds
   ( either normal or 2x as fast as configured ), speed would change both on the client and the server, but not at the
   same time.

This is not a priority at the moment, I'll get back to it once the known issues are solved.

## Feedback

I'm posting this repo mainly to ask for help. If you have any clues, advice, questions or comments, please feel free to contact me:

* on Twitter [@elementbound](https://twitter.com/elementbound)
* on Reddit [/u/elementbound](https://www.reddit.com/user/elementbound)
* by creating an [issue](https://github.com/elementbound/unity-multiplayer-test/issues) under this repo

## License

All scripts under `Assets/Scripts` are licensed under the MIT license.

See [LICENSE](LICENSE).
