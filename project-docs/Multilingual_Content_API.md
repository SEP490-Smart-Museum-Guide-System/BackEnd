# Vietnamese and English content

SMGS keeps the existing Vietnamese museum and artifact fields as the fallback. English entries are stored in `museum_translations` and `artifact_translations`. Artifact titles, long descriptions, and narration continue to use the reviewed `artifact_contents` workflow.

## Select a language

Public reads accept `?lang=en` or `?lang=vi`; `Accept-Language` also works. For signed-in users, `PUT /api/me/language` stores their preferred language, which is used when a request does not specify one. A missing English translation falls back to Vietnamese. The API reports the language actually returned in each museum/artifact DTO's `LanguageCode`.

## Add or update a museum translation

The caller must be allowed to manage the museum:

```http
PUT /api/museums/{museumId}/translations/en
Content-Type: application/json
```

```json
{
  "name": "Museum name in English",
  "description": "Museum description in English",
  "address": "Museum address in English",
  "openingHours": "Opening hours in English"
}
```

Use `/vi` to update the existing Vietnamese fields. Coordinates, city code, image, and dates remain shared.

## Add or update an artifact name

The caller must be allowed to manage the artifact:

```http
PUT /api/artifacts/{artifactId}/translations/en
Content-Type: application/json
```

```json
{ "name": "Artifact name in English" }
```

Use `/vi` to update the existing Vietnamese name. To translate the artifact's title, full description, and narration, continue submitting an English content version with `LanguageCode: "en"` to `/api/artifacts/{artifactId}/contents/manual`; a curator must approve it before it is published. The public published-content endpoint falls back to the Vietnamese published version when the requested English version is not yet available.

## Database update

Apply the new EF Core migrations to the intended database after checking the target schema and backup policy. The repository's `project-docs/smgs_current_schema.sql` is the idempotent schema script for a new empty database.
