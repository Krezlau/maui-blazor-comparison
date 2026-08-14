using System.Globalization;
using System.Text.Json.Serialization;

namespace MgrCode.MockServer;

public sealed record TickerDto(
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("lastPrice")] string LastPrice,
    [property: JsonPropertyName("indexPrice")] string IndexPrice,
    [property: JsonPropertyName("markPrice")] string MarkPrice,
    [property: JsonPropertyName("prevPrice24h")] string PrevPrice24h,
    [property: JsonPropertyName("price24hPcnt")] string Price24hPcnt,
    [property: JsonPropertyName("highPrice24h")] string HighPrice24h,
    [property: JsonPropertyName("lowPrice24h")] string LowPrice24h,
    [property: JsonPropertyName("openPrice24h")] string OpenPrice24h,
    [property: JsonPropertyName("turnover24h")] string Turnover24h,
    [property: JsonPropertyName("volume24h")] string Volume24h,
    [property: JsonPropertyName("bid1Price")] string Bid1Price,
    [property: JsonPropertyName("bid1Size")] string Bid1Size,
    [property: JsonPropertyName("ask1Price")] string Ask1Price,
    [property: JsonPropertyName("ask1Size")] string Ask1Size);

public sealed record TickersResult(
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("list")] List<TickerDto> List);

public sealed record TickersResponse(
    [property: JsonPropertyName("retCode")] int RetCode,
    [property: JsonPropertyName("retMsg")] string RetMsg,
    [property: JsonPropertyName("result")] TickersResult Result,
    [property: JsonPropertyName("time")] long Time);

public sealed record WsSubscribeResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("ret_msg")] string RetMsg,
    [property: JsonPropertyName("conn_id")] string ConnId,
    [property: JsonPropertyName("req_id")] string? ReqId,
    [property: JsonPropertyName("op")] string Op,
    [property: JsonPropertyName("args")] string[] Args);

public sealed record WsPong(
    [property: JsonPropertyName("op")] string Op,
    [property: JsonPropertyName("ts")] long Ts);

public sealed record WsTickerUpdate(
    [property: JsonPropertyName("topic")] string Topic,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("ts")] long Ts,
    [property: JsonPropertyName("data")] TickerDto Data);
