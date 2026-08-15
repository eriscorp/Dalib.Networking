// SPDX-License-Identifier: MIT
// (C) 2026 ERISCO, LLC. See LICENSE and CONTRIBUTORS.md.

namespace DALib.Networking.Packets.Client;

/// <summary>
///     The action byte (after the <c>0x01</c> gate and the <c>u32</c> shop id) that selects a C->S 0x54
///     <see cref="PlayerShopActionPacket" /> form and its tail. Sent by the client while an
///     employee/consignment shop window is open; the counterpart to the S->C 0x4F
///     <see cref="DALib.Networking.Packets.Server.PlayerShopType" /> the server pushes.
/// </summary>
/// <remarks>
///     Every value below was observed with a known trigger by live capture against the retail 7.41
///     client on 2026-08-14. The tail <em>structures</em> were already correct from binary work; the
///     0/1 assignment was inverted here until that capture, so <c>0</c> is the add and <c>1</c> is the
///     withdraw, not the other way round.
/// </remarks>
public enum PlayerShopActionType : byte
{
    /// <summary>0 - add an item to the shop by dragging it from an inventory slot. Tail
    ///     <c>[u8 InventorySlot][u32 Quantity]</c>. The destination shop slot never reaches the wire;
    ///     the server assigns it.</summary>
    AddItem = 0,

    /// <summary>1 - withdraw from the shop. Tail <c>[u32 ListingId][u32 Amount][u16 0][u16 0]</c>,
    ///     where a <c>ListingId</c> of 0 addresses the gold till (the lock icon beside the shop's
    ///     gold). Only the gold form has been observed; no UI route to a non-zero listing id is
    ///     known.</summary>
    Withdraw = 1,

    /// <summary>2 - apply a listing's terms. Tail <c>[u32 ListingId][u32 Price][u32 Count]</c>.</summary>
    UpdateListing = 2,

    /// <summary>3 - remove a listing, by id. Tail <c>[u32 ListingId]</c>. The CLR button.</summary>
    RemoveListing = 3,

    /// <summary>4 - the Dismiss button. No tail. Not a close: the window's OK button dismisses it
    ///     without sending anything.</summary>
    Dismiss = 4,

    /// <summary>5 - the shop-opened handshake, auto-sent from the window's constructor. No tail.</summary>
    ShopOpened = 5
}
