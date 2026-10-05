import { Injectable, signal } from '@angular/core';

export type UiLang = 'da' | 'en';

/**
 * Runtime UI strings (da default; UK-English secondary). Recipe content and
 * scraped ingredient text stay verbatim from the source site â€” this covers
 * interface chrome only.
 */
const STRINGS: Record<string, Record<UiLang, string>> = {
  'app.name': { da: 'Opskriftsbogen', en: 'RecipeApp' },

  'nav.recipes': { da: 'Opskrifter', en: 'Recipes' },
  'nav.plan': { da: 'Madplan', en: 'Meal plan' },
  'nav.shopping': { da: 'IndkÃ¸b', en: 'Shopping' },
  'nav.seasonal': { da: 'SÃ¦son', en: 'In season' },
  'nav.new': { da: 'Ny opskrift', en: 'New recipe' },
  'nav.logout': { da: 'Log ud', en: 'Log out' },

  'login.title': { da: 'Opskriftsbogen', en: 'RecipeApp' },
  'login.sub': { da: 'Log ind med familiens kodeord', en: 'Log in with the family password' },
  'login.ph': { da: 'Kodeord', en: 'Password' },
  'login.submit': { da: 'Log ind', en: 'Log in' },
  'login.busy': { da: 'Logger indâ€¦', en: 'Logging inâ€¦' },
  'login.wrong': { da: 'Forkert kodeord.', en: 'Wrong password.' },
  'login.server': {
    da: 'Serveren mangler husholdnings-kodeord (Household:Password).',
    en: 'Server has no household password configured (Household:Password).',
  },

  'list.searchPh': { da: 'SÃ¸g i titler eller tagsâ€¦', en: 'Search titles or tagsâ€¦' },
  'list.newBtn': { da: '+ Ny opskrift (link)', en: '+ New recipe (link)' },
  'list.fetching': { da: 'Henterâ€¦', en: 'Loadingâ€¦' },
  'list.error': { da: 'Kunne ikke hente opskrifter.', en: 'Could not load recipes.' },
  'list.none': { da: 'Ingen opskrifter endnu â€” indsÃ¦t et link ovenfor.', en: 'No recipes yet â€” paste a link above.' },
  'list.min': { da: 'min', en: 'min' },
  'list.pers': { da: 'pers.', en: 'servs.' },
  'list.season': { da: 'sÃ¦son', en: 'in season' },

  'detail.servings': { da: 'portioner', en: 'servings' },
  'detail.prepFor': { da: 'min forberedelse', en: 'min prep' },
  'detail.cook': { da: 'min tilberedning', en: 'min cooking' },
  'detail.ingredients': { da: 'Ingredienser', en: 'Ingredients' },
  'detail.howTo': { da: 'SÃ¥dan laver du', en: 'Instructions' },
  'detail.comments': { da: 'Kommentarer', en: 'Comments' },
  'detail.edit': { da: 'Rediger', en: 'Edit' },
  'detail.source': { da: 'Kilde â†-', en: 'Source â†-' },
  'detail.delete': { da: 'Slet', en: 'Delete' },
  'detail.confirmDelete': { da: 'Slet opskriften?', en: 'Delete this recipe?' },
  'detail.addComment': { da: 'TilfÃ¸j', en: 'Add' },
  'detail.none': { da: 'Ingen kommentarer endnu.', en: 'No comments yet.' },
  'detail.authorPh': { da: 'Hvem (valgfri)', en: 'Who (optional)' },
  'detail.textPh': { da: 'Skriv en noteâ€¦', en: 'Write a noteâ€¦' },
  'detail.fetching': { da: 'Henterâ€¦', en: 'Loadingâ€¦' },
  'detail.error': { da: 'Kunne ikke hente opskriften.', en: 'Could not load the recipe.' },

  'form.extractTitle': { da: 'Ny opskrift fra link', en: 'New recipe from link' },
  'form.urlPh': { da: 'SÃ¦t link her (opskriftsside eller YouTube)â€¦', en: 'Paste link here (recipe page or YouTube)â€¦' },
  'form.detailsBtn': { da: 'IndsÃ¦t detaljer', en: 'Fetch details' },
  'form.working': { da: 'Henterâ€¦', en: 'Fetchingâ€¦' },
  'form.transcript': { da: 'Undertekster', en: 'Captions' },
  'form.description': { da: 'Beskrivelse', en: 'Description' },
  'form.rawHint': { da: 'â€” strukturÃ©r manuelt eller justÃ©r', en: 'â€” structure manually below or adjust' },
  'form.title': { da: 'Titel', en: 'Title' },
  'form.desc': { da: 'Kort beskrivelse', en: 'Short description' },
  'form.source': { da: 'Kildelink', en: 'Source link' },
  'form.servings': { da: 'Portioner', en: 'Servings' },
  'form.prep': { da: 'Forbered (min)', en: 'Prep (min)' },
  'form.cook': { da: 'Tilbered (min)', en: 'Cook (min)' },
  'form.tags': { da: 'Tags (kommasepareret)', en: 'Tags (comma-separated)' },
  'form.tagsPh': { da: 'f.eks. aftensmad, vegetar, efterÃ¥r', en: 'e.g. dinner, vegetarian, autumn' },
  'form.seasonal': { da: 'Vis med â€œsÃ¦sonâ€-mÃ¦rke', en: 'Mark as in-season' },
  'form.image': { da: 'Billede', en: 'Image' },
  'form.chooseImage': { da: 'VÃ¦lg billede', en: 'Choose image' },
  'form.removeImage': { da: 'Fjern uploadet billede', en: 'Remove uploaded image' },
  'form.saveFirstUpload': { da: 'Upload krÃ¦ver at opskriften er gemt fÃ¸rst.', en: 'Save the recipe first to upload.' },
  'form.saveFirstRemove': { da: 'Gem opskriften fÃ¸rst.', en: 'Save the recipe first.' },
  'form.ingredients': { da: 'Ingredienser', en: 'Ingredients' },
  'form.addIngredient': { da: '+ TilfÃ¸j ingrediens', en: '+ Add ingredient' },
  'form.qtyPh': { da: 'mÃ¦ngde', en: 'amount' },
  'form.unitPh': { da: 'enhed', en: 'unit' },
  'form.namePh': { da: 'vare', en: 'item' },
  'form.howTo': { da: 'SÃ¥dan laver du', en: 'Instructions' },
  'form.addStep': { da: '+ TilfÃ¸j trin', en: '+ Add step' },
  'form.save': { da: 'Gem opskrift', en: 'Save recipe' },
  'form.saveChanges': { da: 'Gem Ã¦ndringer', en: 'Save changes' },
  'form.saving': { da: 'Gemmerâ€¦', en: 'Savingâ€¦' },
  'form.cancel': { da: 'AnnullÃ©r', en: 'Cancel' },
  'form.needTitle': { da: 'Opskriften skal have en titel.', en: 'The recipe needs a title.' },
  'form.saveFail': { da: 'Kunne ikke gemme opskriften.', en: 'Could not save the recipe.' },
  'form.extractFail': { da: 'UdtrÃ¦kning fejlede â€” udfyld formularen manuelt.', en: 'Extraction failed â€” fill in the form manually.' },
  'form.loadFail': { da: 'Kunne ikke hente opskriften.', en: 'Could not load the recipe.' },
  'form.uploadFail': { da: 'Billede-upload fejlede (max 5 MB, jpg/png/webp).', en: 'Image upload failed (max 5 MB, jpg/png/webp).' },
  'form.imageDeleteFail': { da: 'Fejl ved billedslet.', en: 'Failed to delete the image.' },
  'form.fetching': { da: 'Henterâ€¦', en: 'Loadingâ€¦' },

  'plan.chooseTitle': { da: 'VÃ¦lg opskrift', en: 'Choose recipe' },
  'plan.filterPh': { da: 'FiltrÃ©r opskrifterâ€¦', en: 'Filter recipesâ€¦' },
  'plan.noMatch': { da: 'Ingen match â€” tilfÃ¸j opskrifter fÃ¸rst.', en: 'No match â€” add recipes first.' },
  'plan.thisWeek': { da: 'Denne uge', en: 'This week' },
  'plan.remove': { da: 'Fjern', en: 'Remove' },
  'plan.updating': { da: 'Opdatererâ€¦', en: 'Updatingâ€¦' },
  'plan.slot1': { da: 'Morgenmad', en: 'Breakfast' },
  'plan.slot2': { da: 'Frokost', en: 'Lunch' },
  'plan.slot3': { da: 'Aftensmad', en: 'Dinner' },
  'plan.slot4': { da: 'Ekstra', en: 'Extra' },
  'plan.day.0': { da: 'mandag', en: 'Monday' },
  'plan.day.1': { da: 'tirsdag', en: 'Tuesday' },
  'plan.day.2': { da: 'onsdag', en: 'Wednesday' },
  'plan.day.3': { da: 'torsdag', en: 'Thursday' },
  'plan.day.4': { da: 'fredag', en: 'Friday' },
  'plan.day.5': { da: 'lÃ¸rdag', en: 'Saturday' },
  'plan.day.6': { da: 'sÃ¸ndag', en: 'Sunday' },

  'shopping.generate': { da: 'GenerÃ©r fra madplan', en: 'Generate from meal plan' },
  'shopping.noneYet': {
    da: 'Ingen indkÃ¸bsliste for denne uge endnu. FÃ¸j opskrifter til madplanen â€” listen genereres automatisk.',
    en: 'No shopping list for this week yet. Add recipes to the meal plan â€” the list is generated automatically.',
  },
  'shopping.empty': { da: 'Listen er tom.', en: 'The list is empty.' },
  'shopping.addYourself': { da: 'TilfÃ¸j selv', en: 'Add your own' },
  'shopping.itemPh': { da: 'Vare (f.eks. kaffe)â€¦', en: 'Item (e.g. coffee)â€¦' },
  'shopping.unitPh': { da: 'eventuel mÃ¦ngde/enhed', en: 'optional amount/unit' },
  'shopping.add': { da: 'TilfÃ¸j', en: 'Add' },
  'shopping.existsOrError': { da: 'Fandtes allerede eller fejl.', en: 'Already on the list or failed.' },
  'shopping.loadFail': { da: 'Kunne ikke hente indkÃ¸bslisten.', en: 'Could not load the shopping list.' },
  'shopping.genFail': { da: 'Kunne ikke (gen)generere listen.', en: 'Could not (re)generate the list.' },
  'shopping.meal': { da: 'mÃ¥ltid', en: 'meal' },
  'shopping.meals': { da: 'mÃ¥ltider', en: 'meals' },

  'seasonal.title': { da: 'SÃ¦son i Danmark', en: 'In season in Denmark' },
  'seasonal.sub': {
    da: 'Danske grÃ¸ntsager i sÃ¦son lige nu â€” brug dem, mens de er pÃ¥ toppen.',
    en: 'Danish vegetables in season right now â€” use them while they peak.',
  },
  'seasonal.recipes': { da: 'SÃ¦son-opskrifter', en: 'In-season recipes' },
  'seasonal.none': {
    da: 'Ingen inspiration endnu â€” markÃ©r en opskrift med â€œsÃ¦sonâ€-mÃ¦rket i redigeringsfeltet.',
    en: 'Nothing yet â€” mark a recipe as in-season in the edit form.',
  },
};

@Injectable({ providedIn: 'root' })
export class Lang {
  private readonly langSignal = signal<UiLang>(this.initial());

  /** Current UI language â€” recipes themselves stay verbatim from their source. */
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
