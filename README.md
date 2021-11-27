# Netwind

Netwind aims to be a package that provides a simple framework to implement client-side prediction with server reconciliation.

The project uses [Unity Netcode for GameObjects](https://docs-multiplayer.unity3d.com/docs/getting-started/about). The
concepts should be transferrable to almost any of the major networking packages for Unity - in particular I've seen
Mirror to be quite similar.

> **NOTE:** this is very much a work in progress projects. Docs, samples, etc. coming as soon as the core features are done.
> Until then, refer to the Unity project in this repo as example.

## How to run

To test multiple instances running of the same project, [ParrelSync](https://github.com/VeriorPies/ParrelSync) is used.
After downloading the project, you can create a clone, open it in a separate editor and run one of the instances as
host/server and the other as client.

## How it works

1. Clients gather their inputs
1. Input is submitted to the server
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

> **NOTE:** The frame offset is configurable, i.e. t-1 and t-2 are not hardcoded

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

## Feedback

With any questions or comments, please feel free to contact me:

* on Twitter [@elementbound](https://twitter.com/elementbound)
* on Reddit [/u/elementbound](https://www.reddit.com/user/elementbound)
* by creating an [issue](https://github.com/elementbound/unity-multiplayer-test/issues) under this repo

## License

Netwind is licensed under the MIT license.

See [LICENSE](LICENSE).
