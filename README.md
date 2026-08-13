# DALib.Networking

DALib.Networking is a typed packet library for the DOOMVAS v1 network protocol —
the protocol spoken by *Dark Ages* and related games — written in C# and
targeting .NET 9 and .NET 10.

It has no dependencies beyond the base class library. No image stack, no native
assets: a server, client, or proxy can speak the protocol without pulling in
[DALib](https://github.com/eriscorp/dalib)'s asset and rendering layers.

## Install

```
dotnet add package DALib.Networking
```

## What it does

**Wire framing and codec**

`PacketCodec` encodes typed packets into wire bytes and parses wire bytes back
into typed packets, in both directions. One codec is built per process; it is
stateless and shared. `PacketSession` pairs it with a single connection's crypto
state so callers don't thread that state through every call.

**Cryptography**

CRC-16-CCITT, per-connection `CryptoState` (ordinals, key table, encryption
seed), and the dialog obfuscation layer that wraps opcodes 0x39 and 0x3A. The
encryption method is derived from the opcode — packets do not declare it, and
the codec routes each one for you.

**The opcode surface**

Every client-to-server and server-to-client packet is modeled as a record with
named fields, each documented against the retail client's own behavior.
Discriminated bodies (dialogs, merchant menus, boards, exchanges, player shops)
are modeled as an abstract base plus sealed per-form variants.

**Extension points**

Consumers register their own packet types by attribute and hand their assembly
to the codec, which discovers them alongside the built-in ones:

```csharp
var codec = new PacketCodec([typeof(IPacket).Assembly, typeof(MyPacket).Assembly]);
```

## Usage

```csharp
using DALib.Networking.Crypto;
using DALib.Networking.Packets.Client;
using DALib.Networking.Wire;

// One codec per process; one session per connection.
var codec = new PacketCodec();
var session = new PacketSession(codec, new CryptoState());

ReadOnlyMemory<byte> wire = session.EncodeClient(
    new ClientExitPacket { Signal = ExitSignal.Request });

IServerPacket packet = session.ParseServerPacket(received);
```

## Relationship to DALib

[DALib](https://github.com/eriscorp/dalib) is the sibling library for the game's
*data* formats — `.dat` archives, maps, palettes, sprites, and rendering. The two
packages are independent: neither depends on the other, and they version
separately. Networking lived inside DALib through its beta line and moved here at
1.0.0.

## License

MIT, matching DALib. See [LICENSE](LICENSE) and
[CONTRIBUTORS.md](CONTRIBUTORS.md).

(C) 2026 ERISCO, LLC.
