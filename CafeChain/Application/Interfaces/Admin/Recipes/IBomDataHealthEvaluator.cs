using CafeChain.Application.DTOs.Costing;
using CafeChain.Models.Drinks;
using CafeChain.ViewModels.Admin.Recipes;

namespace CafeChain.Application.Interfaces.Admin.Recipes
{
    public interface IBomDataHealthEvaluator
    {
        BomHealthStatusVM EvaluateConfiguration(Recipe recipe);

        BomHealthStatusVM EvaluateCosting(CostCalculationResult result);
    }
}
