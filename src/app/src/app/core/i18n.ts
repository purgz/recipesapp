import { Injectable, signal } from '@angular/core';

export type UiLang = 'da' | 'en';

/**
 * Runtime UI strings (da default; UK-English secondary). Recipe content and
 * scraped ingredient text stay verbatim from the source site. This covers
 * interface chrome only.
 */
const STRINGS: Record<string, Record<UiLang, string>> = {
  'app.name': { da: 'Opskriftsbogen', en: 'RecipeApp' },

  'nav.recipes': { da: 'Opskrifter', en: 'Recipes' },
  'nav.plan': { da: 'Madplan', en: 'Meal plan' },
  'nav.shopping': { da: 'Indkøb', en: 'Shopping' },
  'nav.seasonal': { da: 'Sæson', en: 'In season' },
  'nav.new': { da: 'Ny opskrift', en: 'New recipe' },
  'nav.logout': { da: 'Log ud', en: 'Log out' },

  'login.title': { da: 'Opskriftsbogen', en: 'RecipeApp' },
  'login.sub': { da: 'Log ind med familiens kodeord', en: 'Log in with the family password' },
  'login.ph': { da: 'Kodeord', en: 'Password' },
  'login.submit': { da: 'Log ind', en: 'Log in' },
  'login.busy': { da: 'Logger ind...', en: 'Logging in...' },
  'login.wrong': { da: 'Forkert kodeord.', en: 'Wrong password.' },
  'login.server': {
    da: 'Serveren mangler husholdnings-kodeord (Household:Password).',
    en: 'Server has no household password configured (Household:Password).',
  },

  'list.searchPh': { da: 'Søg i titler, tags eller ingredienser...', en: 'Search titles, tags or ingredients...' },
  'list.newBtn': { da: '+ Ny opskrift (link)', en: '+ New recipe (link)' },
  'list.fetching': { da: 'Henter...', en: 'Loading...' },
  'list.error': { da: 'Kunne ikke hente opskrifter.', en: 'Could not load recipes.' },
  'list.none': { da: 'Ingen opskrifter endnu - tilføj en fra et link eller manuelt.', en: 'No recipes yet - add one from a link or manually.' },
  'list.min': { da: 'min', en: 'min' },
  'list.pers': { da: 'pers.', en: 'servs.' },
  'list.season': { da: 'sæson', en: 'in season' },

  'detail.servings': { da: 'portioner', en: 'servings' },
  'detail.prepFor': { da: 'min forberedelse', en: 'min prep' },
  'detail.cook': { da: 'min tilberedning', en: 'min cooking' },
  'detail.ingredients': { da: 'Ingredienser', en: 'Ingredients' },
  'detail.howTo': { da: 'Sådan laver du', en: 'Instructions' },
  'detail.comments': { da: 'Kommentarer', en: 'Comments' },
  'detail.edit': { da: 'Rediger', en: 'Edit' },
  'detail.source': { da: 'Kilde', en: 'Source' },
  'detail.delete': { da: 'Slet', en: 'Delete' },
  'detail.confirmDelete': { da: 'Slet opskriften?', en: 'Delete this recipe?' },
  'detail.addComment': { da: 'Tilføj', en: 'Add' },
  'detail.none': { da: 'Ingen kommentarer endnu.', en: 'No comments yet.' },
  'detail.commentFail': { da: 'Kunne ikke gemme kommentaren.', en: 'Could not save the comment.' },
  'detail.authorPh': { da: 'Hvem (valgfri)', en: 'Who (optional)' },
  'detail.textPh': { da: 'Skriv en note...', en: 'Write a note...' },
  'detail.fetching': { da: 'Henter...', en: 'Loading...' },
  'detail.error': { da: 'Kunne ikke hente opskriften.', en: 'Could not load the recipe.' },

  'form.extractTitle': { da: 'Ny opskrift fra link', en: 'New recipe from link' },
  'form.urlPh': { da: 'Sæt link her til en opskriftsside...', en: 'Paste a link to a recipe page here...' },
  'form.detailsBtn': { da: 'Indsæt detaljer', en: 'Fetch details' },
  'form.working': { da: 'Henter...', en: 'Fetching...' },
  'form.title': { da: 'Titel', en: 'Title' },
  'form.desc': { da: 'Kort beskrivelse', en: 'Short description' },
  'form.source': { da: 'Kildelink', en: 'Source link' },
  'form.servings': { da: 'Portioner', en: 'Servings' },
  'form.prep': { da: 'Forbered (min)', en: 'Prep (min)' },
  'form.cook': { da: 'Tilbered (min)', en: 'Cook (min)' },
  'form.tags': { da: 'Tags (kommasepareret)', en: 'Tags (comma-separated)' },
  'form.tagsPh': { da: 'f.eks. aftensmad, vegetar, efterår', en: 'e.g. dinner, vegetarian, autumn' },
  'form.seasonal': { da: 'Vis med "sæson"-mærke', en: 'Mark as in-season' },
  'form.image': { da: 'Billede', en: 'Image' },
  'form.chooseImage': { da: 'Vælg billede', en: 'Choose image' },
  'form.removeImage': { da: 'Fjern uploadet billede', en: 'Remove uploaded image' },
  'form.saveFirstUpload': { da: 'Gem opskriften før du uploader billede.', en: 'Save the recipe first to upload.' },
  'form.saveFirstRemove': { da: 'Gem opskriften først.', en: 'Save the recipe first.' },
  'form.ingredients': { da: 'Ingredienser', en: 'Ingredients' },
  'form.addIngredient': { da: '+ Tilføj ingrediens', en: '+ Add ingredient' },
  'form.qtyPh': { da: 'mængde', en: 'amount' },
  'form.unitPh': { da: 'enhed', en: 'unit' },
  'form.namePh': { da: 'vare', en: 'item' },
  'form.howTo': { da: 'Sådan laver du', en: 'Instructions' },
  'form.addStep': { da: '+ Tilføj trin', en: '+ Add step' },
  'form.save': { da: 'Gem opskrift', en: 'Save recipe' },
  'form.saveChanges': { da: 'Gem ændringer', en: 'Save changes' },
  'form.saving': { da: 'Gemmer...', en: 'Saving...' },
  'form.cancel': { da: 'Annullér', en: 'Cancel' },
  'form.needTitle': { da: 'Opskriften skal have en titel.', en: 'The recipe needs a title.' },
  'form.saveFail': { da: 'Kunne ikke gemme opskriften.', en: 'Could not save the recipe.' },
  'form.extractFail': { da: 'Udtrækning fejlede - udfyld formularen manuelt.', en: 'Extraction failed - fill in the form manually.' },
  'form.loadFail': { da: 'Kunne ikke hente opskriften.', en: 'Could not load the recipe.' },
  'form.uploadFail': { da: 'Billede-upload fejlede (max 5 MB, jpg/png/webp).', en: 'Image upload failed (max 5 MB, jpg/png/webp).' },
  'form.imageDeleteFail': { da: 'Fejl ved billedslet.', en: 'Failed to delete the image.' },
  'form.fetching': { da: 'Henter...', en: 'Loading...' },

  'plan.chooseTitle': { da: 'Vælg opskrift', en: 'Choose recipe' },
  'plan.filterPh': { da: 'Filtrér opskrifter...', en: 'Filter recipes...' },
  'plan.noMatch': { da: 'Ingen match - tilføj opskrifter først.', en: 'No match - add recipes first.' },
  'plan.thisWeek': { da: 'Denne uge', en: 'This week' },
  'plan.remove': { da: 'Fjern', en: 'Remove' },
  'plan.updating': { da: 'Opdaterer...', en: 'Updating...' },
  'shopping.groupsTitle': { da: 'Ugens opskrifter (grupperet per opskrift)', en: "This week's recipes (ingredient groups)" },
  'plan.ingredientsTitle': { da: 'Ugens ingredienser (grupperet per opskrift)', en: "This week's ingredients (grouped per recipe)" },
  'plan.slot1': { da: 'Morgenmad', en: 'Breakfast' },
  'plan.slot2': { da: 'Frokost', en: 'Lunch' },
  'plan.slot3': { da: 'Aftensmad', en: 'Dinner' },
  'plan.slot4': { da: 'Ekstra', en: 'Extra' },
  'plan.day.0': { da: 'mandag', en: 'Monday' },
  'plan.day.1': { da: 'tirsdag', en: 'Tuesday' },
  'plan.day.2': { da: 'onsdag', en: 'Wednesday' },
  'plan.day.3': { da: 'torsdag', en: 'Thursday' },
  'plan.day.4': { da: 'fredag', en: 'Friday' },
  'plan.day.5': { da: 'lørdag', en: 'Saturday' },
  'plan.day.6': { da: 'søndag', en: 'Sunday' },

  'shopping.generate': { da: 'Generér fra madplan', en: 'Generate from meal plan' },
  'shopping.noneYet': {
    da: 'Ingen indkøbsliste for denne uge endnu. Føj opskrifter til madplanen - listen genereres automatisk.',
    en: 'No shopping list for this week yet. Add recipes to the meal plan - the list is generated automatically.',
  },
  'shopping.empty': { da: 'Listen er tom.', en: 'The list is empty.' },
  'shopping.addYourself': { da: 'Tilføj selv', en: 'Add your own' },
  'shopping.itemPh': { da: 'Vare (f.eks. kaffe)...', en: 'Item (e.g. coffee)...' },
  'shopping.unitPh': { da: 'eventuel mængde/enhed', en: 'optional amount/unit' },
  'shopping.add': { da: 'Tilføj', en: 'Add' },
  'shopping.existsOrError': { da: 'Fandtes allerede eller fejl.', en: 'Already on the list or failed.' },
  'shopping.loadFail': { da: 'Kunne ikke hente indkøbslisten.', en: 'Could not load the shopping list.' },
  'shopping.genFail': { da: 'Kunne ikke (gen)generere listen.', en: 'Could not (re)generate the list.' },
  'shopping.meal': { da: 'måltid', en: 'meal' },
  'shopping.meals': { da: 'måltider', en: 'meals' },
  'shopping.hideDone': { da: 'Skjul købte', en: 'Hide done' },
  'shopping.showDone': { da: 'Vis købte', en: 'Show done' },

  'seasonal.title': { da: 'Sæson i Danmark', en: 'In season in Denmark' },
  'seasonal.sub': {
    da: 'Danske grøntsager i sæson lige nu - brug dem, mens de er på toppen.',
    en: 'Danish vegetables in season right now - use them while they peak.',
  },
  'seasonal.recipes': { da: 'Sæson-opskrifter', en: 'In-season recipes' },
  'seasonal.none': {
    da: 'Ingen inspiration endnu - markér en opskrift med "sæson"-mærket i redigeringsfeltet.',
    en: 'Nothing yet - mark a recipe as in-season in the edit form.',
  },
  'seasonal.loadFail': {
    da: 'Kunne ikke hente sæson-opskrifter.',
    en: 'Could not load in-season recipes.',
  },
};

@Injectable({ providedIn: 'root' })
export class Lang {
  private readonly langSignal = signal<UiLang>(this.initial());

  /** Current UI language - recipes themselves stay verbatim from their source. */
  readonly current = this.langSignal.asReadonly();

  t(key: string): string {
    const entry = STRINGS[key];
    if (!entry) {
      return key;
    }
    return entry[this.langSignal()];
  }

  set(lang: UiLang): void {
    this.langSignal.set(lang);
    localStorage.setItem('recipeapp.lang', lang);
    document.documentElement.lang = lang;
  }

  toggle(): void {
    this.set(this.langSignal() === 'da' ? 'en' : 'da');
  }

  private initial(): UiLang {
    const stored = localStorage.getItem('recipeapp.lang');
    if (stored === 'da' || stored === 'en') {
      return stored;
    }
    const nav = typeof navigator !== 'undefined' ? navigator.language.toLowerCase() : 'da';
    return nav.startsWith('da') ? 'da' : 'en';
  }
}
