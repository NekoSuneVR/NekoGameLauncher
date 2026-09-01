# Custom deals endpoint contract

Neko Game Launcher can read extra deal feeds without rebuilding the app.

Add the endpoint in **Settings → Custom deals endpoint**. The endpoint may return either a JSON array directly, or an object with an array in one of these properties:

- `deals`
- `offers`
- `games`
- `data`
- `results`

Example:

```json
{
  "deals": [
    {
      "id": "my-game-1",
      "title": "Example Game",
      "store": "My Store",
      "salePrice": "0.00",
      "normalPrice": "19.99",
      "isFree": true,
      "savings": 100,
      "url": "https://example.com/claim/my-game-1",
      "thumbnail": "https://example.com/images/my-game-1.jpg",
      "description": "Free this week",
      "endsAt": "2026-09-07T18:00:00Z"
    }
  ]
}
```

The parser also accepts common aliases such as `name`, `price`, `normal_price`, `dealUrl`, `image`, `platform`, `discount`, and `expires`.

## Security notes

Only `http://` and `https://` endpoints are accepted. Endpoints are treated as data only: the launcher does not execute code received from a deals feed. Deal links are opened through the user's default browser.
