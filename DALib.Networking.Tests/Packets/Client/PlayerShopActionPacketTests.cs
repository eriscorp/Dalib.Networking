// SPDX-License-Identifier: MIT
// (C) 2026 ERISCO, LLC. See LICENSE and CONTRIBUTORS.md.

using DALib.Networking.Crypto;
using DALib.Networking.Packets.Client;
using DALib.Networking.Wire;

namespace DALib.Networking.Tests.Packets.Client;

/// <summary>
///     Coverage for 0x54 PlayerShopAction (C->S) - the actions driven in an open player-run shop, the pair
///     for S->C 0x4F PlayerShop. Pins each action's wire layout (the shared <c>[u8 0x01 gate][u32 ShopId]
///     [u8 action]</c> prefix + tail), verifies action dispatch and the gate-byte guard, and round-trips
///     through the codec.
/// </summary>
public class PlayerShopActionPacketTests
{
    private static CryptoState MakeCrypto() => new()
    {
        EncryptionSeed = 5,
        EncryptionKey = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09],
    };

    // ---- layout pins --------------------------------------------------------------------------

    // Every vector below is a frame captured from a retail 7.41 client on 2026-08-14, driven by a pushed
    // S->C 0x4F (Oghma HTOO-423). They are bytes the client actually emitted, not a restatement of what
    // this library writes - which is the point, since the library's action mapping was wrong until then.

    [Fact]
    public void AddItem_WriteBody_MatchesLiveCapture()
    {
        // Dragging the item in inventory slot 3 into the shop window: 0100000001000300000001
        new AddShopItemPacket { ShopId = 1, InventorySlot = 3, Quantity = 1 }
            .ToBody().Should().Equal(
                0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01);
    }

    [Fact]
    public void AddItem_LeadingByteIsTheInventorySlot()
    {
        // Slots 1/2/3 walked the leading tail byte 1/2/3 with the quantity fixed at 1, which is what
        // identifies it as the inventory slot rather than a selector or a destination.
        foreach (var slot in (byte[]) [1, 2, 3])
            new AddShopItemPacket { ShopId = 1, InventorySlot = slot, Quantity = 1 }
                .ToBody().Should().Equal(
                    0x01, 0x00, 0x00, 0x00, 0x01, 0x00, slot, 0x00, 0x00, 0x00, 0x01);
    }

    [Fact]
    public void Withdraw_WriteBody_MatchesLiveCapture()
    {
        // Lock icon -> "take back" 1337 gold: 010000000101000000000000053900000000
        // ListingId 0 addresses the gold till; 1337 = 0x539.
        new WithdrawFromShopPacket { ShopId = 1, ListingId = 0, Amount = 1337 }
            .ToBody().Should().Equal(
                0x01, 0x00, 0x00, 0x00, 0x01, 0x01,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x39, 0x00, 0x00, 0x00, 0x00);
    }

    [Fact]
    public void UpdateListing_WriteBody_MatchesLiveCapture()
    {
        // Sell = 1337 confirmed on listing 1: 010000000102000000010000053900000001
        new UpdateShopListingPacket { ShopId = 1, ListingId = 1, Price = 1337, Count = 1 }
            .ToBody().Should().Equal(
                0x01, 0x00, 0x00, 0x00, 0x01, 0x02,
                0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x05, 0x39, 0x00, 0x00, 0x00, 0x01);
    }

    [Fact]
    public void UpdateListing_MiddleFieldIsThePrice()
    {
        // The Sell field moved the middle u32 (100 -> 1337) while the third stayed at the quantity of 1.
        // That ordering is price-then-count, which was an open question before the capture.
        new UpdateShopListingPacket { ShopId = 1, ListingId = 1, Price = 100, Count = 1 }
            .ToBody().Should().Equal(
                0x01, 0x00, 0x00, 0x00, 0x01, 0x02,
                0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x64, 0x00, 0x00, 0x00, 0x01);
    }

    [Fact]
    public void RemoveListing_WriteBody_MatchesLiveCapture()
    {
        // CLR on listing 1: 01000000010300000001
        new RemoveShopListingPacket { ShopId = 1, ListingId = 1 }
            .ToBody().Should().Equal(0x01, 0x00, 0x00, 0x00, 0x01, 0x03, 0x00, 0x00, 0x00, 0x01);
    }

    [Fact]
    public void Dismiss_WriteBody_MatchesLiveCapture()
    {
        // The Dismiss button: 010000000104. The OK button closes the window and sends nothing.
        new DismissShopPacket { ShopId = 1 }
            .ToBody().Should().Equal(0x01, 0x00, 0x00, 0x00, 0x01, 0x04);
    }

    [Fact]
    public void ShopOpened_WriteBody_IsPrefixOnly()
    {
        new ShopOpenedPacket { ShopId = 1 }
            .ToBody().Should().Equal(0x01, 0x00, 0x00, 0x00, 0x01, 0x05);
    }

    // ---- action dispatch ----------------------------------------------------------------------

    [Fact]
    public void Parse_Action0_IsAddItem()
    {
        // The captured add-item frame, parsed back.
        var parsed = PlayerShopActionPacket
            .Parse([0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01])
            .Should().BeOfType<AddShopItemPacket>().Subject;

        parsed.ShopId.Should().Be(1u);
        parsed.InventorySlot.Should().Be((byte)3);
        parsed.Quantity.Should().Be(1u);
    }

    [Fact]
    public void Parse_Action1_IsWithdraw()
    {
        // The captured gold-withdrawal frame, parsed back.
        var parsed = PlayerShopActionPacket
            .Parse([0x01, 0x00, 0x00, 0x00, 0x01, 0x01,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x39, 0x00, 0x00, 0x00, 0x00])
            .Should().BeOfType<WithdrawFromShopPacket>().Subject;

        parsed.ListingId.Should().Be(0u);
        parsed.Amount.Should().Be(1337u);
        parsed.Reserved1.Should().Be((ushort)0);
        parsed.Reserved2.Should().Be((ushort)0);
    }

    [Fact]
    public void Parse_Action2_IsUpdateListing()
    {
        var parsed = PlayerShopActionPacket
            .Parse([0x01, 0x00, 0x00, 0x00, 0x01, 0x02,
                0x00, 0x00, 0x00, 0x2A, 0x00, 0x00, 0x03, 0xE8, 0x00, 0x00, 0x00, 0x05])
            .Should().BeOfType<UpdateShopListingPacket>().Subject;

        parsed.ListingId.Should().Be(42u);
        parsed.Price.Should().Be(1000u);
        parsed.Count.Should().Be(5u);
    }

    [Fact]
    public void Parse_Action3_IsRemoveListing()
    {
        var parsed = PlayerShopActionPacket
            .Parse([0x01, 0x00, 0x00, 0x00, 0x01, 0x03, 0x00, 0x00, 0x00, 0x2A])
            .Should().BeOfType<RemoveShopListingPacket>().Subject;

        parsed.ListingId.Should().Be(42u);
    }

    [Fact]
    public void Parse_BadGateByte_Throws()
    {
        // Second body byte is not the hard-coded 0x01 gate.
        var act = () => PlayerShopActionPacket.Parse([0x00, 0x00, 0x00, 0x00, 0x01, 0x04]);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Parse_UnknownAction_Throws()
    {
        // action 6 - past the known set
        var act = () => PlayerShopActionPacket.Parse([0x01, 0x00, 0x00, 0x00, 0x01, 0x06]);

        act.Should().Throw<InvalidDataException>();
    }

    // ---- round-trip through the codec ---------------------------------------------------------

    [Theory]
    [MemberData(nameof(RoundTripCases))]
    public void RoundTrip_ThroughCodec_PreservesForm(PlayerShopActionPacket original)
    {
        var codec = new PacketCodec();
        var crypto = MakeCrypto();

        var wire = codec.EncodeClient(original, crypto);
        var parsed = codec.ParseClientPacket(wire, crypto);

        parsed.Should().Be(original); // records: value equality across all fields + exact type
    }

    public static TheoryData<PlayerShopActionPacket> RoundTripCases() =>
    [
        new AddShopItemPacket { ShopId = 0x0A0B0C0D, InventorySlot = 7, Quantity = 50_000 },
        new WithdrawFromShopPacket { ShopId = 42, ListingId = 7, Amount = 900, Reserved1 = 0, Reserved2 = 0 },
        new UpdateShopListingPacket { ShopId = 42, ListingId = 3, Price = 1_000_000, Count = 250 },
        new RemoveShopListingPacket { ShopId = 42, ListingId = 3 },
        new DismissShopPacket { ShopId = 42 },
        new ShopOpenedPacket { ShopId = 42 },
    ];
}
