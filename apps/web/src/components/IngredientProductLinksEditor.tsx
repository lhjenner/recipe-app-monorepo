import type { SupermarketChain } from '../services/ingredientsApi';
import type { IngredientEditorState } from '../ingredientFormModel';

interface IngredientProductLinksEditorProps {
  links: IngredientEditorState['productLinks'];
  chains: SupermarketChain[];
  errorFor: (field: string) => string | undefined;
  onChange: (links: IngredientEditorState['productLinks']) => void;
}

function IngredientProductLinksEditor({
  links,
  chains,
  errorFor,
  onChange,
}: IngredientProductLinksEditorProps) {
  return (
    <fieldset>
      <legend>Product links</legend>
      {links.map((link, index) => (
        <div className="ingredient-product-link" key={`${link.chainId}-${index}`}>
          <label className="ingredient-form-field">
            <span>Supermarket chain</span>
            <select
              aria-label={`Product link ${index + 1} chain`}
              value={link.chainId}
              onChange={(event) => onChange(links.map((item, itemIndex) =>
                itemIndex === index ? { ...item, chainId: event.currentTarget.value } : item))}
            >
              {chains.map((chain) => <option key={chain.id} value={chain.id}>{chain.name}</option>)}
            </select>
          </label>
          <label className="ingredient-form-field">
            <span>Product URL</span>
            <input
              type="url"
              aria-label={`Product link ${index + 1} URL`}
              value={link.url}
              onChange={(event) => onChange(links.map((item, itemIndex) =>
                itemIndex === index ? { ...item, url: event.currentTarget.value } : item))}
            />
            {errorFor(`ProductLinks[${index}].Url`) && (
              <span className="ingredient-field-error">{errorFor(`ProductLinks[${index}].Url`)}</span>
            )}
          </label>
          <label className="ingredient-form-field">
            <span>Variant label (optional)</span>
            <input
              value={link.variantLabel}
              onChange={(event) => onChange(links.map((item, itemIndex) =>
                itemIndex === index ? { ...item, variantLabel: event.currentTarget.value } : item))}
            />
          </label>
          <label className="ingredient-form-field">
            <span>Note (optional)</span>
            <input
              value={link.note}
              onChange={(event) => onChange(links.map((item, itemIndex) =>
                itemIndex === index ? { ...item, note: event.currentTarget.value } : item))}
            />
          </label>
          <label className="ingredient-checkbox">
            <input
              type="checkbox"
              checked={link.supportsBothPricingMethods}
              onChange={(event) => onChange(links.map((item, itemIndex) =>
                itemIndex === index
                  ? { ...item, supportsBothPricingMethods: event.currentTarget.checked }
                  : item))}
            />
            <span>Supports both pricing methods</span>
          </label>
          <label className="ingredient-checkbox">
            <input
              type="checkbox"
              checked={link.isPreferred}
              onChange={(event) => onChange(links.map((item, itemIndex) =>
                itemIndex === index ? { ...item, isPreferred: event.currentTarget.checked } : item))}
            />
            <span>Preferred</span>
          </label>
          <button
            className="btn btn-secondary ingredient-remove-link"
            type="button"
            onClick={() => onChange(links.filter((_, itemIndex) => itemIndex !== index))}
          >
            Remove link
          </button>
        </div>
      ))}
      {errorFor('ProductLinks') && <span className="ingredient-field-error">{errorFor('ProductLinks')}</span>}
      <button
        className="btn btn-secondary"
        type="button"
        onClick={() => onChange([...links, {
          chainId: chains[0]?.id ?? '',
          url: '',
          variantLabel: '',
          note: '',
          supportsBothPricingMethods: false,
          isPreferred: false,
        }])}
        disabled={chains.length === 0}
      >
        Add product link
      </button>
    </fieldset>
  );
}

export default IngredientProductLinksEditor;
