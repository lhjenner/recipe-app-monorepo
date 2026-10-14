export interface NavigationItem {
  label: string;
  route: string;
  pageTitle: string;
}

export const MOBILE_BREAKPOINT = 700;

export const navigationItems: readonly NavigationItem[] = [
  { label: 'Home', route: '/home', pageTitle: 'Home' },
  { label: 'Recipe List', route: '/recipes', pageTitle: 'Recipe List' },
  { label: 'Add Recipe', route: '/recipes/new', pageTitle: 'Add Recipe' },
  { label: 'Grocery List', route: '/grocery-list', pageTitle: 'Grocery List' },
  { label: 'Meal Planner', route: '/meal-planner', pageTitle: 'Meal Planner' },
  { label: 'Food Diary', route: '/food-diary', pageTitle: 'Food Diary' },
  { label: 'Ingredient Maintenance', route: '/ingredient-maintenance', pageTitle: 'Ingredient Maintenance' },
  { label: 'Archived Recipes', route: '/recipes/archived', pageTitle: 'Archived Recipes' },
];

export function getActiveNavigationItem(pathname: string): NavigationItem | undefined {
  return navigationItems
    .filter((item) => pathname === item.route || pathname.startsWith(`${item.route}/`))
    .sort((first, second) => second.route.length - first.route.length)[0];
}
