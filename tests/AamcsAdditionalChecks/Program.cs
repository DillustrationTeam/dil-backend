using System.Text.Json;
using ArtCommission.Application.Chat.Commands.SendMessage;
using ArtCommission.Application.Chat.Commands.TranslateMessage;
using ArtCommission.Application.Chat.Commands.UploadAttachment;
using ArtCommission.Application.Chat.Common;
using ArtCommission.Application.Chat.Queries.GetChatRoom;
using ArtCommission.Application.Payment.Commands.CreateBankAccount;
using ArtCommission.Application.Payment.Commands.CreatePayoutRequest;
using ArtCommission.Application.Payment.Commands.DeleteBankAccount;
using ArtCommission.Application.Payment.Commands.HandlePaymentWebhook;
using ArtCommission.Application.Payment.Commands.UpdateBankAccount;
using ArtCommission.Application.Payment.Queries.GetBankAccounts;
using ArtCommission.Application.Payment.Queries.GetPaymentOrders;
using ArtCommission.Application.Wallets.Queries.GetMyWallet;
using ArtCommission.Application.Wallets.Queries.GetMyWalletTransactions;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using ArtCommission.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase("aamcs-additional-" + Guid.NewGuid()).Options);

var checks = 0;
void Pass(bool condition, string code)
{
    if (!condition) throw new Exception($"{code}: check failed");
    checks++;
    Console.WriteLine($"PASS {code}-UTCID06");
}

void Case(bool condition, string code, int id)
{
    if (!condition) throw new Exception($"{code}-UTCID{id:00}: check failed");
    checks++;
    Console.WriteLine($"PASS {code}-UTCID{id:00}");
}

var userId = Guid.NewGuid();
var otherUserId = Guid.NewGuid();

var roomService = new ChatRoomService(db, NullLogger<ChatRoomService>.Instance);
var missingRoom = await new GetChatRoomQueryHandler(db, roomService)
    .Handle(new GetChatRoomQuery(userId, Guid.NewGuid()), default);
Pass(!missingRoom.Item1 && missingRoom.Item2 is null, "AAMCS-059");

Pass(!new SendMessageCommandValidator().Validate(
    new SendMessageCommand(userId, Guid.NewGuid(), " ")).IsValid, "AAMCS-060");

Pass(!new UploadAttachmentCommandValidator().Validate(
    new UploadAttachmentCommand(userId, Guid.NewGuid(), Stream.Null, "run.exe", "application/x-msdownload", 1)).IsValid,
    "AAMCS-061");

Pass(!new TranslateMessageCommandValidator().Validate(
    new TranslateMessageCommand(userId, Guid.NewGuid(), "xx")).IsValid, "AAMCS-062");

var emptyWallet = await new GetMyWalletQueryHandler(db)
    .Handle(new GetMyWalletQuery(userId), default);
Pass(emptyWallet.Item1 && emptyWallet.Item2?.Wallet.Balance == 0m, "AAMCS-095");

db.Wallets.Add(new Wallet { UserId = userId, Balance = 0m });
await db.SaveChangesAsync();
var badTransactionFilter = await new GetMyWalletTransactionsQueryHandler(db)
    .Handle(new GetMyWalletTransactionsQuery(userId, WalletTxType: "invalid"), default);
Pass(!badTransactionFilter.Item1 && badTransactionFilter.Item2 is null, "AAMCS-096");

using var json = JsonDocument.Parse("{}");
var badGateway = await new HandlePaymentWebhookCommandHandler(
        db, [], null!, NullLogger<HandlePaymentWebhookCommandHandler>.Instance)
    .Handle(new HandlePaymentWebhookCommand("unknown", json.RootElement), default);
Pass(badGateway.HttpStatus == 400 && badGateway.Data is null, "AAMCS-097");

var badPaymentFilter = await new GetPaymentOrdersQueryHandler(db)
    .Handle(new GetPaymentOrdersQuery(userId, Status: "invalid"), default);
Pass(!badPaymentFilter.Item1 && badPaymentFilter.Item2 is null, "AAMCS-098");

db.BankAccounts.AddRange(
    new BankAccount { UserId = userId, BankName = "A", AccountNumber = "111", AccountHolder = "USER" },
    new BankAccount { UserId = otherUserId, BankName = "B", AccountNumber = "222", AccountHolder = "OTHER" });
await db.SaveChangesAsync();
var ownBanks = await new GetBankAccountsQueryHandler(db)
    .Handle(new GetBankAccountsQuery(userId), default);
Pass(ownBanks.Item1 && ownBanks.Item2?.Count == 1 && ownBanks.Item2[0].BankName == "A", "AAMCS-099");

Pass(!new CreateBankAccountCommandValidator().Validate(
    new CreateBankAccountCommand(userId, "Bank", "bad account!", "User")).IsValid, "AAMCS-100");

var updateOther = await new UpdateBankAccountCommandHandler(db)
    .Handle(new UpdateBankAccountCommand(userId, db.BankAccounts.Single(x => x.UserId == otherUserId).Id,
        "Bank", "333", "User"), default);
Pass(!updateOther.Item1 && updateOther.Item2 is null, "AAMCS-101");

var deleteMissing = await new DeleteBankAccountCommandHandler(db)
    .Handle(new DeleteBankAccountCommand(userId, Guid.NewGuid()), default);
Pass(!deleteMissing.Item1 && !deleteMissing.Item2, "AAMCS-102");

Pass(!new CreatePayoutRequestCommandValidator().Validate(
    new CreatePayoutRequestCommand(userId, 0m, Guid.NewGuid())).IsValid, "AAMCS-103");

// AAMCS-059: workroom access boundaries.
var emptyRoom = await roomService.ResolveRoomAsync(Guid.Empty, userId);
Case(emptyRoom.Room is null && emptyRoom.Error is not null, "AAMCS-059", 1);
var anonymousRoom = await roomService.ResolveRoomAsync(Guid.NewGuid(), Guid.Empty);
Case(anonymousRoom.Room is null && anonymousRoom.Error is not null, "AAMCS-059", 2);
var unknownRoom = await roomService.ResolveRoomAsync(Guid.NewGuid(), userId);
Case(unknownRoom.Room is null, "AAMCS-059", 3);
Case(!await roomService.IsMemberAsync(Guid.NewGuid(), userId), "AAMCS-059", 4);
var missingRoomAgain = await new GetChatRoomQueryHandler(db, roomService)
    .Handle(new GetChatRoomQuery(otherUserId, Guid.NewGuid()), default);
Case(!missingRoomAgain.Item1 && missingRoomAgain.Item2 is null, "AAMCS-059", 5);

// AAMCS-060: message validation.
var messageValidator = new SendMessageCommandValidator();
Case(messageValidator.Validate(new SendMessageCommand(userId, Guid.NewGuid(), "Hello")).IsValid, "AAMCS-060", 1);
Case(!messageValidator.Validate(new SendMessageCommand(userId, Guid.NewGuid(), " ")).IsValid, "AAMCS-060", 2);
Case(!messageValidator.Validate(new SendMessageCommand(userId, Guid.NewGuid(), new string('x', SendMessageCommandValidator.MaxBodyLength + 1))).IsValid, "AAMCS-060", 3);
Case(!messageValidator.Validate(new SendMessageCommand(userId, Guid.NewGuid(), "Hello", "Executable")).IsValid, "AAMCS-060", 4);
Case(!messageValidator.Validate(new SendMessageCommand(userId, Guid.Empty, "Hello")).IsValid, "AAMCS-060", 5);

// AAMCS-061: attachment validation.
var attachmentValidator = new UploadAttachmentCommandValidator();
Case(attachmentValidator.Validate(new UploadAttachmentCommand(userId, Guid.NewGuid(), Stream.Null, "image.png", "image/png", 1)).IsValid, "AAMCS-061", 1);
Case(!attachmentValidator.Validate(new UploadAttachmentCommand(userId, Guid.NewGuid(), Stream.Null, "run.exe", "application/x-msdownload", 1)).IsValid, "AAMCS-061", 2);
Case(!attachmentValidator.Validate(new UploadAttachmentCommand(userId, Guid.NewGuid(), Stream.Null, "big.zip", "application/zip", UploadAttachmentCommandValidator.MaxFileSizeBytes + 1)).IsValid, "AAMCS-061", 3);
Case(!attachmentValidator.Validate(new UploadAttachmentCommand(userId, Guid.NewGuid(), Stream.Null, "empty.txt", "text/plain", 0)).IsValid, "AAMCS-061", 4);
Case(!attachmentValidator.Validate(new UploadAttachmentCommand(userId, Guid.NewGuid(), Stream.Null, "", "image/png", 1)).IsValid, "AAMCS-061", 5);

// AAMCS-062: translation request validation.
var translationValidator = new TranslateMessageCommandValidator();
Case(translationValidator.Validate(new TranslateMessageCommand(userId, Guid.NewGuid(), "vi")).IsValid, "AAMCS-062", 1);
Case(!translationValidator.Validate(new TranslateMessageCommand(userId, Guid.NewGuid(), "xx")).IsValid, "AAMCS-062", 2);
Case(!translationValidator.Validate(new TranslateMessageCommand(userId, Guid.Empty, "en")).IsValid, "AAMCS-062", 3);
Case(!translationValidator.Validate(new TranslateMessageCommand(userId, Guid.NewGuid(), "")).IsValid, "AAMCS-062", 4);
Case(translationValidator.Validate(new TranslateMessageCommand(userId, Guid.NewGuid(), "EN")).IsValid, "AAMCS-062", 5);

// AAMCS-095: wallet ownership and empty-wallet behavior.
Case(emptyWallet.Item1 && emptyWallet.Item2?.Wallet.WalletId == Guid.Empty, "AAMCS-095", 1);
var wallet = db.Wallets.Single(x => x.UserId == userId);
wallet.Balance = 125_000m;
wallet.LockedBalance = 25_000m;
await db.SaveChangesAsync();
var ownedWallet = await new GetMyWalletQueryHandler(db).Handle(new GetMyWalletQuery(userId), default);
Case(ownedWallet.Item2?.Wallet.Balance == 125_000m, "AAMCS-095", 2);
Case(ownedWallet.Item2?.Summary.EscrowHeldAmount == 25_000m, "AAMCS-095", 3);
var otherWallet = await new GetMyWalletQueryHandler(db).Handle(new GetMyWalletQuery(otherUserId), default);
Case(otherWallet.Item2?.Wallet.Balance == 0m, "AAMCS-095", 4);
wallet.IsDeleted = true;
await db.SaveChangesAsync();
var deletedWallet = await new GetMyWalletQueryHandler(db).Handle(new GetMyWalletQuery(userId), default);
Case(deletedWallet.Item2?.Wallet.WalletId == Guid.Empty, "AAMCS-095", 5);
wallet.IsDeleted = false;
await db.SaveChangesAsync();

// AAMCS-096: wallet transaction filters and boundaries.
var txHandler = new GetMyWalletTransactionsQueryHandler(db);
Case(!(await txHandler.Handle(new GetMyWalletTransactionsQuery(userId, Direction: "sideways"), default)).Item1, "AAMCS-096", 1);
Case(!(await txHandler.Handle(new GetMyWalletTransactionsQuery(userId, From: DateTimeOffset.UtcNow, To: DateTimeOffset.UtcNow.AddDays(-1)), default)).Item1, "AAMCS-096", 2);
Case(!(await txHandler.Handle(new GetMyWalletTransactionsQuery(userId, Cursor: "bad-cursor"), default)).Item1, "AAMCS-096", 3);
Case((await txHandler.Handle(new GetMyWalletTransactionsQuery(otherUserId), default)).Item3 == 0, "AAMCS-096", 4);
Case((await txHandler.Handle(new GetMyWalletTransactionsQuery(userId, Limit: 0), default)).Item1, "AAMCS-096", 5);

// AAMCS-097: webhook rejection happens before any wallet mutation.
var webhookHandler = new HandlePaymentWebhookCommandHandler(db, [], null!, NullLogger<HandlePaymentWebhookCommandHandler>.Instance);
Case((await webhookHandler.Handle(new HandlePaymentWebhookCommand(null, json.RootElement), default)).HttpStatus == 400, "AAMCS-097", 1);
Case((await webhookHandler.Handle(new HandlePaymentWebhookCommand("", json.RootElement), default)).HttpStatus == 400, "AAMCS-097", 2);
Case((await webhookHandler.Handle(new HandlePaymentWebhookCommand("unknown", json.RootElement), default)).HttpStatus == 400, "AAMCS-097", 3);
Case((await webhookHandler.Handle(new HandlePaymentWebhookCommand("PAYOS-UNKNOWN", json.RootElement), default)).HttpStatus == 400, "AAMCS-097", 4);
Case(db.Wallets.Single(x => x.UserId == userId).Balance == 125_000m, "AAMCS-097", 5);

// AAMCS-098: payment history validation and empty ownership scope.
var paymentHandler = new GetPaymentOrdersQueryHandler(db);
Case(!(await paymentHandler.Handle(new GetPaymentOrdersQuery(userId, Gateway: "invalid"), default)).Item1, "AAMCS-098", 1);
Case(!(await paymentHandler.Handle(new GetPaymentOrdersQuery(userId, Cursor: "bad-cursor"), default)).Item1, "AAMCS-098", 2);
Case((await paymentHandler.Handle(new GetPaymentOrdersQuery(userId), default)).Item2?.Items.Count == 0, "AAMCS-098", 3);
Case((await paymentHandler.Handle(new GetPaymentOrdersQuery(otherUserId), default)).Item2?.Items.Count == 0, "AAMCS-098", 4);
Case((await paymentHandler.Handle(new GetPaymentOrdersQuery(userId, Limit: 0), default)).Item1, "AAMCS-098", 5);

// AAMCS-099: only active accounts owned by the caller are returned.
Case(ownBanks.Item2?.All(x => x.BankName == "A") == true, "AAMCS-099", 1);
var otherBanks = await new GetBankAccountsQueryHandler(db).Handle(new GetBankAccountsQuery(otherUserId), default);
Case(otherBanks.Item2?.Count == 1 && otherBanks.Item2[0].BankName == "B", "AAMCS-099", 2);
var ownerAccount = db.BankAccounts.Single(x => x.UserId == userId);
ownerAccount.IsDeleted = true;
await db.SaveChangesAsync();
Case((await new GetBankAccountsQueryHandler(db).Handle(new GetBankAccountsQuery(userId), default)).Item2?.Count == 0, "AAMCS-099", 3);
Case((await new GetBankAccountsQueryHandler(db).Handle(new GetBankAccountsQuery(Guid.NewGuid()), default)).Item2?.Count == 0, "AAMCS-099", 4);
ownerAccount.IsDeleted = false;
await db.SaveChangesAsync();
Case((await new GetBankAccountsQueryHandler(db).Handle(new GetBankAccountsQuery(userId), default)).Item2?[0].AccountNumberMasked.Contains('*') == true, "AAMCS-099", 5);

// AAMCS-100: create-bank validation.
var createBankValidator = new CreateBankAccountCommandValidator();
Case(createBankValidator.Validate(new CreateBankAccountCommand(userId, "VCB", "012345", "CHAU VNB")).IsValid, "AAMCS-100", 1);
Case(!createBankValidator.Validate(new CreateBankAccountCommand(userId, "", "012345", "CHAU VNB")).IsValid, "AAMCS-100", 2);
Case(!createBankValidator.Validate(new CreateBankAccountCommand(userId, "VCB", "", "CHAU VNB")).IsValid, "AAMCS-100", 3);
Case(!createBankValidator.Validate(new CreateBankAccountCommand(userId, "VCB", "bad account!", "CHAU VNB")).IsValid, "AAMCS-100", 4);
Case(!createBankValidator.Validate(new CreateBankAccountCommand(userId, "VCB", "012345", "")).IsValid, "AAMCS-100", 5);

// AAMCS-101: update-bank validation and ownership.
var updateValidator = new UpdateBankAccountCommandValidator();
Case(updateValidator.Validate(new UpdateBankAccountCommand(userId, ownerAccount.Id, "VCB", "012345", "CHAU VNB")).IsValid, "AAMCS-101", 1);
Case(!updateValidator.Validate(new UpdateBankAccountCommand(userId, Guid.Empty, "VCB", "012345", "CHAU VNB")).IsValid, "AAMCS-101", 2);
Case(!updateValidator.Validate(new UpdateBankAccountCommand(userId, ownerAccount.Id, "VCB", "bad account!", "CHAU VNB")).IsValid, "AAMCS-101", 3);
Case(!(await new UpdateBankAccountCommandHandler(db).Handle(new UpdateBankAccountCommand(userId, Guid.NewGuid(), "VCB", "012345", "CHAU VNB"), default)).Item1, "AAMCS-101", 4);
Case(!updateOther.Item1, "AAMCS-101", 5);

// AAMCS-102: delete-bank ownership and idempotent not-found behavior.
var deleteHandler = new DeleteBankAccountCommandHandler(db);
Case(!deleteMissing.Item1 && !deleteMissing.Item2, "AAMCS-102", 1);
Case(!(await deleteHandler.Handle(new DeleteBankAccountCommand(userId, db.BankAccounts.Single(x => x.UserId == otherUserId).Id), default)).Item1, "AAMCS-102", 2);
var disposable = new BankAccount { UserId = userId, BankName = "C", AccountNumber = "333", AccountHolder = "USER" };
db.BankAccounts.Add(disposable);
await db.SaveChangesAsync();
Case((await deleteHandler.Handle(new DeleteBankAccountCommand(userId, disposable.Id), default)).Item1, "AAMCS-102", 3);
Case(db.BankAccounts.Single(x => x.Id == disposable.Id).IsDeleted, "AAMCS-102", 4);
Case(!(await deleteHandler.Handle(new DeleteBankAccountCommand(userId, disposable.Id), default)).Item1, "AAMCS-102", 5);

// AAMCS-103: payout request validation boundaries.
var payoutValidator = new CreatePayoutRequestCommandValidator();
Case(payoutValidator.Validate(new CreatePayoutRequestCommand(userId, 50_000m, ownerAccount.Id)).IsValid, "AAMCS-103", 1);
Case(!payoutValidator.Validate(new CreatePayoutRequestCommand(userId, 0m, ownerAccount.Id)).IsValid, "AAMCS-103", 2);
Case(!payoutValidator.Validate(new CreatePayoutRequestCommand(userId, -1m, ownerAccount.Id)).IsValid, "AAMCS-103", 3);
Case(!payoutValidator.Validate(new CreatePayoutRequestCommand(userId, 50_000m, Guid.Empty)).IsValid, "AAMCS-103", 4);
Case(!payoutValidator.Validate(new CreatePayoutRequestCommand(userId, 50_000m, ownerAccount.Id, new string('x', 501))).IsValid, "AAMCS-103", 5);

Console.WriteLine($"Passed {checks} AAMCS additional checks.");
