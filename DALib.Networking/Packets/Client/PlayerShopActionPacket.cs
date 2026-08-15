// SPDX-License-Identifier: MIT
// (C) 2026 ERISCO, LLC. See LICENSE and CONTRIBUTORS.md.

using System;
using System.IO;
using DALib.Networking.Wire;

namespace DALib.Networking.Packets.Client;

/// <summary>
///     0x54 (C->S) - an action taken in an open player-run shop (the employee/consignment shop window). The
///     C->S counterpart to S->C 0x4F <see cref="DALib.Networking.Packets.Server.PlayerShopPacket" />: the
///     server pushes the shop's state with 0x4F, and the client drives it with 0x54. The body opens with a
///     shared prefix <c>[u8 0x01 gate][u32 BE ShopId][u8 action]</c>; the action byte (a
///     <see cref="PlayerShopActionType" />) selects the form and any tail. The concrete forms are the sealed
///     records deriving from this base (<see cref="AddShopItemPacket" />,
///     <see cref="WithdrawFromShopPacket" />, <see cref="UpdateShopListingPacket" />,
///     <see cref="RemoveShopListingPacket" />, <see cref="DismissShopPacket" />,
///     <see cref="ShopOpenedPacket" />).
/// </summary>
/// <remarks>
///     <para>
///         The gate byte is a hard-coded <c>0x01</c> (the same gate the S->C 0x4F validator checks);
///         <see cref="Parse" /> rejects anything else. The <see cref="ShopId" /> is the session token echoed
///         from the 0x4F that opened the window.
///     </para>
///     <para>
///         No reference server parses 0x54 today (Hybrasyl has no handler), so every form here is modeled for
///         wire completeness. The wire <em>structure</em> of each form is binary-verified in both the 7.41 and
///         5.51 retail clients, where the layouts are byte-for-byte identical.
///     </para>
///     <para>
///         The field <em>semantics</em> were inferred until 2026-08-14, when every action byte was observed
///         with a known trigger by driving a live retail 7.41 client from a pushed 0x4F. That capture
///         corrected the action mapping - <c>0</c> and <c>1</c> had been assigned the wrong way round - and
///         settled action 2's trailing pair as price-then-count. Fields still unpinned say so individually.
///     </para>
/// </remarks>
[ClientOpcode(ClientOpcode.PlayerShopAction)]
public abstract record PlayerShopActionPacket : ClientPacket
{
    /// <summary>The hard-coded gate byte that leads every 0x54 body; the server drops a body whose gate byte
    ///     is not <c>0x01</c>.</summary>
    public const byte GateByte = 0x01;

    /// <summary>The action byte that selects this variant's form.</summary>
    public abstract PlayerShopActionType ShopActionType { get; }

    /// <summary>The shop's id, the session token echoed from the S->C 0x4F that opened the window.</summary>
    public required uint ShopId { get; init; }

    /// <inheritdoc />
    public override byte Opcode => (byte)ClientOpcode.PlayerShopAction;

    /// <summary>Writes the shared <c>[u8 0x01 gate][u32 BE ShopId][u8 action]</c> prefix. Variants call this,
    ///     then append their tail.</summary>
    protected void WritePrefix(IPacketWriter writer)
    {
        writer.WriteByte(GateByte);
        writer.WriteUInt32(ShopId);
        writer.WriteByte((byte)ShopActionType);
    }

    /// <summary>
    ///     Parses a 0x54 body, validating the gate byte and dispatching on the action byte to the matching
    ///     variant. This is the standalone entry and what <see cref="ClientOpcodeAttribute" /> dispatch binds.
    /// </summary>
    public static PlayerShopActionPacket Parse(ReadOnlySpan<byte> body)
    {
        var reader = new PacketReader(body);
        var gate = reader.ReadByte();

        if (gate != GateByte)
            throw new InvalidDataException(
                $"0x54 PlayerShopAction: expected gate byte 0x{GateByte:X2}, got 0x{gate:X2}.");

        var shopId = reader.ReadUInt32();
        var action = (PlayerShopActionType)reader.ReadByte();

        return action switch
        {
            PlayerShopActionType.AddItem => new AddShopItemPacket
                { ShopId = shopId, InventorySlot = reader.ReadByte(), Quantity = reader.ReadUInt32() },
            PlayerShopActionType.Withdraw => new WithdrawFromShopPacket
            {
                ShopId = shopId,
                ListingId = reader.ReadUInt32(),
                Amount = reader.ReadUInt32(),
                Reserved1 = reader.ReadUInt16(),
                Reserved2 = reader.ReadUInt16()
            },
            PlayerShopActionType.UpdateListing => new UpdateShopListingPacket
            {
                ShopId = shopId,
                ListingId = reader.ReadUInt32(),
                Price = reader.ReadUInt32(),
                Count = reader.ReadUInt32()
            },
            PlayerShopActionType.RemoveListing => new RemoveShopListingPacket
                { ShopId = shopId, ListingId = reader.ReadUInt32() },
            PlayerShopActionType.Dismiss => new DismissShopPacket { ShopId = shopId },
            PlayerShopActionType.ShopOpened => new ShopOpenedPacket { ShopId = shopId },
            _ => throw new InvalidDataException(
                $"0x54 PlayerShopAction: unknown action 0x{(byte)action:X2}.")
        };
    }
}

/// <summary>
///     0x54 action 0 - add an item to the shop, dragged in from an inventory slot. Tail
///     <c>[u8 InventorySlot][u32 BE Quantity]</c>.
/// </summary>
/// <remarks>
///     Live-captured against the retail 7.41 client on 2026-08-14: dragging from inventory slots 1, 2 and 3
///     walked <see cref="InventorySlot" /> 1/2/3 while <see cref="Quantity" /> stayed at 1. The destination
///     shop slot never reaches the wire - the server decides where the listing lands. The tail is five
///     bytes; there is no trailing zero byte.
/// </remarks>
public sealed record AddShopItemPacket : PlayerShopActionPacket
{
    /// <summary>The inventory slot the item was dragged from.</summary>
    public required byte InventorySlot { get; init; }

    /// <summary>How many of the stack to list.</summary>
    public required uint Quantity { get; init; }

    /// <inheritdoc />
    public override PlayerShopActionType ShopActionType => PlayerShopActionType.AddItem;

    /// <inheritdoc />
    public override void WriteBody(IPacketWriter writer)
    {
        WritePrefix(writer);
        writer.WriteByte(InventorySlot);
        writer.WriteUInt32(Quantity);
    }
}

/// <summary>
///     0x54 action 1 - withdraw from the shop. Tail
///     <c>[u32 BE ListingId][u32 BE Amount][u16 BE 0][u16 BE 0]</c>.
/// </summary>
/// <remarks>
///     Live-captured 2026-08-14 through the lock icon beside the shop's gold, which opens a "how much money
///     do you wish to take back" prompt: entering 1337 produced
///     <c>[00000000][00000539][00000000]</c>. So <see cref="ListingId" /> <c>0</c> addresses the gold till
///     and <see cref="Amount" /> is the sum taken. No UI route to a non-zero listing id is known, so the
///     item-withdrawal form is unobserved. The trailing four bytes were zero in every capture; they are
///     modeled as two u16s after the binary, but a single <c>u32</c> fits the observations equally well and
///     nothing seen so far distinguishes them.
/// </remarks>
public sealed record WithdrawFromShopPacket : PlayerShopActionPacket
{
    /// <summary>The listing to withdraw from; <c>0</c> addresses the shop's gold till.</summary>
    public required uint ListingId { get; init; }

    /// <summary>The amount to withdraw.</summary>
    public required uint Amount { get; init; }

    /// <summary>A trailing u16 the client always sends as 0. Preserved for round-tripping.</summary>
    public ushort Reserved1 { get; init; }

    /// <summary>A trailing u16 the client always sends as 0. Preserved for round-tripping.</summary>
    public ushort Reserved2 { get; init; }

    /// <inheritdoc />
    public override PlayerShopActionType ShopActionType => PlayerShopActionType.Withdraw;

    /// <inheritdoc />
    public override void WriteBody(IPacketWriter writer)
    {
        WritePrefix(writer);
        writer.WriteUInt32(ListingId);
        writer.WriteUInt32(Amount);
        writer.WriteUInt16(Reserved1);
        writer.WriteUInt16(Reserved2);
    }
}

/// <summary>
///     0x54 action 2 - update a listing's terms. Tail <c>[u32 BE ListingId][u32 BE Price][u32 BE Count]</c>.
///     The client sources <see cref="Price" /> and <see cref="Count" /> from the item-property dialog's
///     edited fields.
/// </summary>
public sealed record UpdateShopListingPacket : PlayerShopActionPacket
{
    /// <summary>The id of the listing to update.</summary>
    public required uint ListingId { get; init; }

    /// <summary>The listing's new price.</summary>
    public required uint Price { get; init; }

    /// <summary>The listing's new count/quantity.</summary>
    public required uint Count { get; init; }

    /// <inheritdoc />
    public override PlayerShopActionType ShopActionType => PlayerShopActionType.UpdateListing;

    /// <inheritdoc />
    public override void WriteBody(IPacketWriter writer)
    {
        WritePrefix(writer);
        writer.WriteUInt32(ListingId);
        writer.WriteUInt32(Price);
        writer.WriteUInt32(Count);
    }
}

/// <summary>
///     0x54 action 3 - remove a listing from the shop, by id. Tail <c>[u32 BE ListingId]</c>.
/// </summary>
public sealed record RemoveShopListingPacket : PlayerShopActionPacket
{
    /// <summary>The id of the listing to remove.</summary>
    public required uint ListingId { get; init; }

    /// <inheritdoc />
    public override PlayerShopActionType ShopActionType => PlayerShopActionType.RemoveListing;

    /// <inheritdoc />
    public override void WriteBody(IPacketWriter writer)
    {
        WritePrefix(writer);
        writer.WriteUInt32(ListingId);
    }
}

/// <summary>
///     0x54 action 4 - the Dismiss button. Prefix only.
/// </summary>
/// <remarks>
///     Not a window close. The shop window's OK button dismisses it without putting anything on the wire,
///     so this packet is the affirmative Dismiss action rather than the act of closing (live-captured
///     2026-08-14).
/// </remarks>
public sealed record DismissShopPacket : PlayerShopActionPacket
{
    /// <inheritdoc />
    public override PlayerShopActionType ShopActionType => PlayerShopActionType.Dismiss;

    /// <inheritdoc />
    public override void WriteBody(IPacketWriter writer) => WritePrefix(writer);
}

/// <summary>
///     0x54 action 5 - the shop-opened handshake, auto-sent from the shop window's constructor when it opens.
///     Prefix only.
/// </summary>
public sealed record ShopOpenedPacket : PlayerShopActionPacket
{
    /// <inheritdoc />
    public override PlayerShopActionType ShopActionType => PlayerShopActionType.ShopOpened;

    /// <inheritdoc />
    public override void WriteBody(IPacketWriter writer) => WritePrefix(writer);
}
